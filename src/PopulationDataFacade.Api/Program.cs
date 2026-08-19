using Duende.AspNetCore.Authentication.JwtBearer.DPoP;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
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

builder.Services.AddPopulationDataFacadeInfrastructure(builder.Configuration);
builder.Services.AddSingleton<IFhirPopulationMapper, FhirPopulationMapper>();
builder.Services.AddDataProtection().SetApplicationName("PopulationDataFacade");
builder.Services.AddOptions<PatientContextOptions>()
    .Bind(builder.Configuration.GetSection(PatientContextOptions.SectionName))
    .Validate(options => options.Lifetime is { Ticks: > 0 } && options.Lifetime <= TimeSpan.FromHours(1),
        "PatientContext:Lifetime must be between zero and one hour.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.HeaderName) &&
                         options.HeaderName.All(character => char.IsAsciiLetterOrDigit(character) || character == '-'),
        "PatientContext:HeaderName must be a non-empty HTTP header token containing letters, digits, or hyphens.")
    .Validate(options => options.TestAliases.All(alias =>
            !string.IsNullOrWhiteSpace(alias.Key) &&
            !string.IsNullOrWhiteSpace(alias.Value.LogicalId) &&
            !string.IsNullOrWhiteSpace(alias.Value.NationalIdentityNumber)),
        "Every PatientContext:TestAliases entry must define an alias, LogicalId, and NationalIdentityNumber.")
    .Validate(options => !builder.Environment.IsProduction() || options.TestAliases.Count == 0,
        "PatientContext:TestAliases must be empty in Production.")
    .ValidateOnStart();
builder.Services.AddSingleton<IPatientContextTokenService, PatientContextTokenService>();
builder.Services.AddScoped<PatientRequestContextFactory>();

var helseId = builder.Configuration.GetSection(HelseIdOptions.SectionName).Get<HelseIdOptions>() ?? new HelseIdOptions();
var dhg = builder.Configuration.GetSection(DhgOptions.SectionName).Get<DhgOptions>() ?? new DhgOptions();
const string authenticationScheme = "HelseId";
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
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                if (context.Response.HasStarted) return;
                context.HandleResponse();
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
builder.Services.AddDistributedMemoryCache();
builder.Services.ConfigureDPoPTokensForScheme(authenticationScheme);
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
builder.Services.AddSwaggerGen();
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
app.UseAuthentication();
app.UseAuthorization();

if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapOpenApi();
}

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready");
app.MapPopulationFhirApi();

app.MapPost("/test/patient-context/{alias}", (
        string alias,
        HttpContext httpContext,
        IWebHostEnvironment environment,
        IPatientContextTokenService tokens,
        Microsoft.Extensions.Options.IOptions<PatientContextOptions> options) =>
    {
        if (environment.IsProduction()) return Results.NotFound();
        var subject = httpContext.User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(subject)) return Results.Unauthorized();
        var token = tokens.Issue(alias, subject);
        var patient = options.Value.TestAliases[alias];
        return Results.Json(new { patientId = patient.LogicalId, patientContext = token });
    })
    .RequireAuthorization("population.read")
    .WithTags("Test support")
    .WithDescription("Issues a short-lived protected context for a configured synthetic DHG Test patient. Disabled in production.");

app.Run();

public partial class Program;
