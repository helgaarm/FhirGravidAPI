using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using PopulationDataFacade.Api.Fhir;
using PopulationDataFacade.Api.Security;
using PopulationDataFacade.Core;
using PopulationDataFacade.Infrastructure;
using PopulationDataFacade.Infrastructure.Configuration;

var builder = WebApplication.CreateBuilder(args);

var developmentTestMode = builder.Configuration
    .GetSection(DevelopmentTestModeOptions.SectionName)
    .Get<DevelopmentTestModeOptions>() ?? new DevelopmentTestModeOptions();
var dhg = builder.Configuration.GetSection(DhgOptions.SectionName).Get<DhgOptions>() ?? new DhgOptions();
var productionSecurityBoundary = builder.Environment.IsProduction() ||
                                 dhg.Environment.Equals("Production", StringComparison.OrdinalIgnoreCase);
var localDevelopmentTestMode = developmentTestMode.Enabled && builder.Environment.IsDevelopment();
if (developmentTestMode.Enabled &&
    (!localDevelopmentTestMode || !dhg.Environment.Equals("Test", StringComparison.OrdinalIgnoreCase)))
    throw new InvalidOperationException(
        "DevelopmentTestMode requires Dhg:Environment=Test and a Development host.");
if (localDevelopmentTestMode)
{
    var configuredListenerUrls = (builder.Configuration["urls"] ?? string.Empty)
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Concat(builder.Configuration.GetSection("Kestrel:Endpoints").GetChildren()
            .Select(endpoint => endpoint["Url"])
            .Where(url => !string.IsNullOrWhiteSpace(url))!);
    if (!string.IsNullOrWhiteSpace(builder.Configuration["HTTP_PORTS"]) ||
        !string.IsNullOrWhiteSpace(builder.Configuration["HTTPS_PORTS"]) ||
        configuredListenerUrls.Any(url => !Program.IsLoopbackListener(url!)))
        throw new InvalidOperationException(
            "DevelopmentTestMode requires loopback-only listener URLs and does not allow HTTP_PORTS/HTTPS_PORTS wildcard bindings.");
}

builder.Services.AddPopulationDataFacadeInfrastructure(builder.Configuration);
builder.Services.AddSingleton<IFhirPopulationMapper, FhirPopulationMapper>();
builder.Services.AddDataProtection().SetApplicationName("PopulationDataFacade");
var forwardedHeadersEnabled = builder.Configuration.GetValue<bool>("ReverseProxy:ForwardedHeadersEnabled");
if (forwardedHeadersEnabled)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });
}
builder.Services.AddOptions<PatientContextOptions>()
    .Bind(builder.Configuration.GetSection(PatientContextOptions.SectionName))
    .Validate(options => options.Lifetime is { Ticks: > 0 } && options.Lifetime <= TimeSpan.FromHours(1),
        "PatientContext:Lifetime must be between zero and one hour.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.HeaderName) &&
                         options.HeaderName.All(character => char.IsAsciiLetterOrDigit(character) || character == '-'),
        "PatientContext:HeaderName must be a non-empty HTTP header token containing letters, digits, or hyphens.")
        .Validate(options => options.TestAliases.All(alias =>
            !string.IsNullOrWhiteSpace(alias.Key) &&
            PatientContextOptions.IsLogicalIdFormat(alias.Value.LogicalId) &&
            !string.IsNullOrWhiteSpace(alias.Value.NationalIdentityNumber) &&
            PatientContextOptions.IsNationalIdentityNumberFormat(alias.Value.NationalIdentityNumber)),
        "Every PatientContext:TestAliases entry must define an alias, a valid FHIR LogicalId, and an 11-digit NationalIdentityNumber.")
    .Validate(options => options.TestAliases.Values
            .Select(patient => patient.NationalIdentityNumber)
            .Distinct(StringComparer.Ordinal)
            .Count() == options.TestAliases.Count,
        "Every PatientContext:TestAliases entry must use a unique NationalIdentityNumber.")
    .Validate(options => PatientContextOptions.HaveUniqueLogicalIds(options.TestAliases),
        "Every PatientContext:TestAliases entry must use a unique, case-sensitive LogicalId.")
    .Validate(options => PatientContextOptions.LogicalIdsDoNotContainNins(options.TestAliases),
        "PatientContext:TestAliases LogicalId values must not equal any configured NationalIdentityNumber.")
    .Validate(options => developmentTestMode.Enabled ||
                         PatientContextOptions.IsPatientIdHmacKeyValid(options.PatientIdHmacKey),
        "PatientContext:PatientIdHmacKey must be a Base64-encoded secret of at least 32 bytes outside DevelopmentTestMode.")
    .Validate(options => !productionSecurityBoundary || options.TestAliases.Count == 0,
        "PatientContext:TestAliases must be empty when the host or DHG environment is Production.")
    .ValidateOnStart();
builder.Services.AddSingleton<IPatientContextTokenService, PatientContextTokenService>();
builder.Services.AddSingleton<IPatientIdPseudonymizer, PatientIdPseudonymizer>();
builder.Services.AddScoped<PatientRequestContextFactory>();

var helseId = builder.Configuration.GetSection(HelseIdOptions.SectionName).Get<HelseIdOptions>() ?? new HelseIdOptions();
const string authenticationScheme = "HelseId";
const string authGatewaySecretHeader = "X-Auth-Gateway-Secret";
var authGatewaySharedSecret = builder.Configuration["AuthGateway:SharedSecret"] ?? string.Empty;
var authGatewaySharedSecretBytes = Encoding.UTF8.GetBytes(authGatewaySharedSecret);
if (!developmentTestMode.Enabled && authGatewaySharedSecretBytes.Length < 32)
    throw new InvalidOperationException("AuthGateway:SharedSecret must contain at least 32 bytes outside DevelopmentTestMode.");
if (developmentTestMode.Enabled)
{
    builder.Services.AddAuthentication();
}
else
{
    builder.Services.AddAuthentication(authenticationScheme)
        .AddJwtBearer(authenticationScheme, options =>
        {
            options.Authority = helseId.Authority.ToString().TrimEnd('/');
            options.Audience = helseId.FacadeAudience;
            options.MapInboundClaims = false;
            options.RequireHttpsMetadata = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                AudienceValidator = (audiences, _, _) =>
                {
                    var audienceValues = audiences?.ToArray() ?? [];
                    return audienceValues.Length == 1 &&
                           audienceValues[0].Equals(helseId.FacadeAudience, StringComparison.Ordinal);
                },
                ValidTypes = ["at+jwt"],
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256, SecurityAlgorithms.RsaSsaPssSha256],
                ClockSkew = TimeSpan.FromSeconds(3)
            };
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var gatewaySecrets = context.Request.Headers[authGatewaySecretHeader];
                    if (gatewaySecrets.Count != 1)
                    {
                        context.Fail("The request did not arrive through the authentication gateway.");
                        return Task.CompletedTask;
                    }
                    var suppliedSecretBytes = Encoding.UTF8.GetBytes(gatewaySecrets[0]!);
                    if (suppliedSecretBytes.Length != authGatewaySharedSecretBytes.Length ||
                        !CryptographicOperations.FixedTimeEquals(suppliedSecretBytes, authGatewaySharedSecretBytes))
                    {
                        context.Fail("The request did not arrive through the authentication gateway.");
                        return Task.CompletedTask;
                    }

                    var values = context.Request.Headers.Authorization;
                    if (values.Count == 0) return Task.CompletedTask;
                    if (values.Count != 1 ||
                        !AuthenticationHeaderValue.TryParse(values[0], out var authorization) ||
                        !authorization.Scheme.Equals("DPoP", StringComparison.OrdinalIgnoreCase) ||
                        string.IsNullOrWhiteSpace(authorization.Parameter))
                    {
                        context.Fail("The Authorization header must contain exactly one DPoP access token.");
                        return Task.CompletedTask;
                    }

                    context.Token = authorization.Parameter;
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    context.Request.Headers.Remove(authGatewaySecretHeader);
                    return Task.CompletedTask;
                },
                OnChallenge = async context =>
                {
                    if (context.Response.HasStarted) return;
                    context.HandleResponse();
                    context.Response.Headers.WWWAuthenticate = "DPoP";
                    await FhirHttp.Result(
                        FhirHttp.Outcome("security", "A valid HelseID DPoP access token is required."),
                        StatusCodes.Status401Unauthorized).ExecuteAsync(context.HttpContext);
                },
                OnForbidden = context => FhirHttp.Result(
                        FhirHttp.Outcome("forbidden", "The token does not grant access to this operation."),
                        StatusCodes.Status403Forbidden)
                    .ExecuteAsync(context.HttpContext)
            };
        });
}
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("population.read", policy => policy
        .RequireAuthenticatedUser()
        .RequireAssertion(context => context.User.FindAll("scope")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(helseId.FacadeScope, StringComparer.Ordinal)));
});
builder.Services.AddSingleton<
    Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler,
    FhirAuthorizationMiddlewareResultHandler>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen(options => options.OperationFilter<PatientContextHeaderOperationFilter>());
builder.Services.AddHealthChecks();

var exportTelemetry = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("PopulationDataFacade"))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation(options =>
                options.FilterHttpRequestMessage = request =>
                    request.RequestUri is null ||
                    !string.Equals(request.RequestUri.Host, dhg.BaseUrl.Host, StringComparison.OrdinalIgnoreCase))
            .AddSource("PopulationDataFacade.Dhg", "PopulationDataFacade.HelseId");
        if (exportTelemetry) tracing.AddOtlpExporter();
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddMeter("PopulationDataFacade.Dhg");
        if (exportTelemetry) metrics.AddOtlpExporter();
    });

var app = builder.Build();

if (forwardedHeadersEnabled) app.UseForwardedHeaders();

app.Use(async (context, next) =>
{
    var supplied = context.Request.Headers["X-Correlation-ID"].ToString();
    context.TraceIdentifier = Guid.TryParse(supplied, out var parsed)
        ? parsed.ToString("D")
        : Guid.NewGuid().ToString("D");
    context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier;
    await next();
});
app.UseMiddleware<FhirExceptionMiddleware>();
if (localDevelopmentTestMode)
{
    app.Use(async (context, next) =>
    {
        var remoteAddress = context.Connection.RemoteIpAddress;
        if (remoteAddress is null || !IPAddress.IsLoopback(remoteAddress))
        {
            await FhirHttp.Result(
                    FhirHttp.Outcome(
                        "forbidden",
                        "DevelopmentTestMode accepts requests only from the local machine."),
                    StatusCodes.Status403Forbidden)
                .ExecuteAsync(context);
            return;
        }

        await next();
    });
}
app.UseAuthentication();
app.UseAuthorization();

var swaggerEnabled = !productionSecurityBoundary ||
                     builder.Configuration.GetValue<bool>("Swagger:EnabledInProduction");
if (swaggerEnabled)
{
    if (productionSecurityBoundary)
    {
        app.UseWhen(
            context => context.Request.Path.StartsWithSegments("/swagger"),
            branch => branch.Use(async (context, next) =>
            {
                var authorization = context.RequestServices
                    .GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationService>();
                var result = await authorization.AuthorizeAsync(context.User, null, "population.read");
                if (result.Succeeded)
                {
                    await next();
                    return;
                }

                var authenticated = context.User.Identity?.IsAuthenticated == true;
                if (!authenticated)
                    context.Response.Headers.WWWAuthenticate = "DPoP";
                await FhirHttp.Result(
                        FhirHttp.Outcome(
                            authenticated ? "forbidden" : "security",
                            authenticated
                                ? "The HelseID token does not grant access to Swagger."
                                : "A valid HelseID DPoP access token is required to use Swagger."),
                        authenticated
                            ? StatusCodes.Status403Forbidden
                            : StatusCodes.Status401Unauthorized)
                    .ExecuteAsync(context);
            }));
    }

    app.UseSwagger();
    app.UseSwaggerUI();
    var openApi = app.MapOpenApi();
    if (productionSecurityBoundary)
        openApi.RequireAuthorization("population.read");
}

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready");
app.MapPopulationFhirApi(requireAuthorization: !developmentTestMode.Enabled);

var patientContextEndpoint = app.MapPost("/test/patient-context/{alias}", (
        string alias,
        HttpContext httpContext,
        IPatientContextTokenService tokens,
        Microsoft.Extensions.Options.IOptions<PatientContextOptions> options) =>
    {
        if (productionSecurityBoundary) return Results.NotFound();
        var subject = developmentTestMode.Enabled
            ? developmentTestMode.Subject
            : httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(subject)) return Results.Unauthorized();
        var token = tokens.Issue(alias, subject);
        var patient = options.Value.TestAliases[alias];
        return Results.Json(new { patientId = patient.LogicalId, patientContext = token });
    })
    .WithTags("Test support")
    .WithDescription(
        "Slår opp et konfigurert alias for en syntetisk testpasient i DHG Test og returnerer en logisk FHIR-pasient-ID og en kortlivet, beskyttet pasientkontekst. Pasient-ID-en er ikke et fødselsnummer. Endepunktet er deaktivert i produksjon.");

if (!developmentTestMode.Enabled)
    patientContextEndpoint.RequireAuthorization("population.read");

app.Run();

public partial class Program
{
    internal static bool IsLoopbackListener(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
        return IPAddress.TryParse(uri.Host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address);
    }
}
