using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace PopulationDataFacade.IntegrationTests;

public sealed class GatewayAuthenticationBoundaryTests : IClassFixture<RealGatewayAuthenticationFactory>
{
    private const string GatewaySecretHeader = "X-Auth-Gateway-Secret";
    private readonly RealGatewayAuthenticationFactory factory;

    public GatewayAuthenticationBoundaryTests(RealGatewayAuthenticationFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Valid_gateway_secret_and_at_jwt_use_the_real_helseid_scheme_and_remove_the_secret()
    {
        using var client = factory.CreateClient();
        using var request = AuthorizedRequest(factory.CreateAccessToken());

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("false", response.Headers.GetValues(GatewaySecretObservationFilter.ResultHeader).Single());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("wrong")]
    [InlineData("duplicate")]
    public async Task Invalid_gateway_secret_is_rejected_by_the_real_helseid_scheme(string scenario)
    {
        using var client = factory.CreateClient();
        using var request = AuthorizedRequest(factory.CreateAccessToken(), includeSecret: false);
        if (scenario == "wrong")
            request.Headers.TryAddWithoutValidation(GatewaySecretHeader, new string('x', 32));
        else if (scenario == "duplicate")
            request.Headers.TryAddWithoutValidation(GatewaySecretHeader, [RealGatewayAuthenticationFactory.SharedSecret, RealGatewayAuthenticationFactory.SharedSecret]);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertFhirUnauthorizedAsync(response);
    }

    [Theory]
    [InlineData("missing-authorization")]
    [InlineData("bearer")]
    [InlineData("duplicate-authorization")]
    [InlineData("invalid-signature")]
    [InlineData("wrong-issuer")]
    [InlineData("wrong-audience")]
    [InlineData("multiple-audiences")]
    [InlineData("expired")]
    [InlineData("missing-expiry")]
    [InlineData("wrong-type")]
    [InlineData("unsupported-algorithm")]
    public async Task Invalid_access_tokens_are_rejected_by_the_real_helseid_scheme(string scenario)
    {
        using var client = factory.CreateClient();
        var token = factory.CreateAccessToken(scenario);
        using var request = AuthorizedRequest(token, includeAuthorization: scenario != "missing-authorization");
        if (scenario == "bearer")
        {
            request.Headers.Remove("Authorization");
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
        }
        else if (scenario == "duplicate-authorization")
        {
            request.Headers.Remove("Authorization");
            request.Headers.TryAddWithoutValidation("Authorization", ["DPoP " + token, "DPoP " + token]);
        }

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        await AssertFhirUnauthorizedAsync(response);
    }

    private static HttpRequestMessage AuthorizedRequest(
        string token,
        bool includeSecret = true,
        bool includeAuthorization = true)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/openapi/v1.json");
        if (includeAuthorization)
            request.Headers.TryAddWithoutValidation("Authorization", "DPoP " + token);
        if (includeSecret)
            request.Headers.TryAddWithoutValidation(GatewaySecretHeader, RealGatewayAuthenticationFactory.SharedSecret);
        return request;
    }

    private static async Task AssertFhirUnauthorizedAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/fhir+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(response.Headers.WwwAuthenticate, challenge => challenge.Scheme == "DPoP");
        Assert.Contains("\"resourceType\":\"OperationOutcome\"", body);
    }
}

public sealed class RealGatewayAuthenticationFactory : WebApplicationFactory<Program>
{
    internal const string Issuer = "https://helseid-sts.test.nhn.no";
    internal const string Audience = "nhn:population-data-facade";
    internal const string Scope = "nhn:population-data-facade/read";
    internal const string SharedSecret = "gateway-integration-secret-32bytes";
    private readonly RsaSecurityKey signingKey;

    public RealGatewayAuthenticationFactory()
    {
        var rsa = RSA.Create(2048);
        signingKey = new RsaSecurityKey(rsa) { KeyId = Guid.NewGuid().ToString("N") };
    }

    internal string CreateAccessToken(string scenario = "valid")
    {
        var now = DateTime.UtcNow;
        var issuer = scenario == "wrong-issuer" ? "https://issuer.example" : Issuer;
        var audience = scenario == "wrong-audience" ? "other-audience" : Audience;
        var signingCredentials = scenario switch
        {
            "invalid-signature" => new SigningCredentials(
                new RsaSecurityKey(RSA.Create(2048)) { KeyId = "other-key" },
                SecurityAlgorithms.RsaSha256),
            "unsupported-algorithm" => new SigningCredentials(
                new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32)),
                SecurityAlgorithms.HmacSha256),
            _ => new SigningCredentials(signingKey, SecurityAlgorithms.RsaSha256)
        };
        var payload = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Iss] = issuer,
            [JwtRegisteredClaimNames.Aud] = scenario == "multiple-audiences"
                ? new[] { Audience, "other-audience" }
                : audience,
            [JwtRegisteredClaimNames.Nbf] = new DateTimeOffset(now.AddSeconds(-1)).ToUnixTimeSeconds(),
            [JwtRegisteredClaimNames.Iat] = new DateTimeOffset(now).ToUnixTimeSeconds(),
            [JwtRegisteredClaimNames.Sub] = "gateway-boundary-test-user",
            ["scope"] = Scope
        };
        if (scenario != "missing-expiry")
            payload[JwtRegisteredClaimNames.Exp] = new DateTimeOffset(
                scenario == "expired" ? now.AddMinutes(-1) : now.AddMinutes(5)).ToUnixTimeSeconds();
        return new JsonWebTokenHandler().CreateToken(
            JsonSerializer.Serialize(payload),
            signingCredentials,
            new Dictionary<string, object>
            {
                [JwtHeaderParameterNames.Typ] = scenario == "wrong-type" ? "JWT" : "at+jwt"
            });
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var privateJwk = ProductionSwaggerFactory.CreatePrivateJwk();
        builder.UseEnvironment("Production");
        builder.UseSetting("AuthGateway:SharedSecret", SharedSecret);
        builder.UseSetting("Swagger:EnabledInProduction", "true");
        builder.UseSetting("HelseId:Authority", Issuer);
        builder.UseSetting("HelseId:FacadeAudience", Audience);
        builder.UseSetting("HelseId:FacadeScope", Scope);
        builder.UseSetting("HelseId:ClientId", "gateway-boundary-integration-client");
        builder.UseSetting("HelseId:ClientAssertionJwk", privateJwk);
        builder.UseSetting("HelseId:DPoPJwk", privateJwk);
        builder.UseSetting("PatientContext:PatientIdHmacKey", ProductionSwaggerFactory.PatientIdHmacKey);
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>("HelseId", options =>
            {
                var configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                configuration.SigningKeys.Add(signingKey);
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                options.TokenValidationParameters.ValidateIssuerSigningKey = true;
                options.TokenValidationParameters.IssuerSigningKey = signingKey;
                options.TokenValidationParameters.ValidIssuer = Issuer;
            });
            services.AddSingleton<IStartupFilter, GatewaySecretObservationFilter>();
        });
    }
}

public sealed class GatewaySecretObservationFilter : IStartupFilter
{
    internal const string ResultHeader = "X-Test-Gateway-Secret-Present";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => application =>
    {
        application.Use(async (context, nextMiddleware) =>
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[ResultHeader] = context.Request.Headers.ContainsKey("X-Auth-Gateway-Secret")
                    ? "true"
                    : "false";
                return Task.CompletedTask;
            });
            await nextMiddleware();
        });
        next(application);
    };
}
