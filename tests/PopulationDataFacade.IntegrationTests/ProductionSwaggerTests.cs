using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace PopulationDataFacade.IntegrationTests;

public sealed class ProductionSwaggerTests
{
    [Fact]
    public void Production_startup_requires_the_auth_gateway_shared_secret()
    {
        using var factory = new ProductionSwaggerFactory(enabled: null, includeGatewaySecret: false);

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("AuthGateway:SharedSecret must contain at least 32 bytes", exception.ToString());
    }

    [Theory]
    [InlineData("/swagger")]
    [InlineData("/swagger/")]
    [InlineData("/swagger/index.html")]
    [InlineData("/swagger/v1/swagger.json")]
    [InlineData("/openapi/v1.json")]
    public async Task Swagger_is_disabled_by_default_in_production(string path)
    {
        await using var factory = new ProductionSwaggerFactory(enabled: null);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/swagger/index.html")]
    [InlineData("/swagger/v1/swagger.json")]
    [InlineData("/openapi/v1.json")]
    public async Task Dhg_production_uses_the_production_security_boundary_on_a_non_production_host(string path)
    {
        await using var factory = new DhgProductionNonProductionHostFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/swagger")]
    [InlineData("/swagger/")]
    [InlineData("/swagger/index.html")]
    [InlineData("/swagger/v1/swagger.json")]
    [InlineData("/openapi/v1.json")]
    public async Task Enabled_production_swagger_requires_helseid(string path)
    {
        await using var factory = new ProductionSwaggerFactory(enabled: true);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, challenge => challenge.Scheme == "DPoP");
    }

    [Theory]
    [InlineData("/swagger/index.html")]
    [InlineData("/swagger/v1/swagger.json")]
    [InlineData("/openapi/v1.json")]
    public async Task Enabled_production_swagger_accepts_the_population_read_policy(string path)
    {
        await using var factory = new ProductionSwaggerFactory(enabled: true);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "integration-token");
        request.Headers.TryAddWithoutValidation("X-Test-Subject", "swagger-production-user");
        request.Headers.TryAddWithoutValidation("X-Test-Scope", "nhn:population-data-facade/read");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/swagger")]
    [InlineData("/swagger/")]
    public async Task Enabled_production_swagger_authorizes_the_ui_redirect(string path)
    {
        await using var factory = new ProductionSwaggerFactory(enabled: true);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "integration-token");
        request.Headers.TryAddWithoutValidation("X-Test-Subject", "swagger-production-user");
        request.Headers.TryAddWithoutValidation("X-Test-Scope", "nhn:population-data-facade/read");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.True((int)response.StatusCode is >= 300 and < 400);
        Assert.EndsWith("index.html", response.Headers.Location?.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/swagger")]
    [InlineData("/swagger/")]
    [InlineData("/swagger/index.html")]
    [InlineData("/swagger/v1/swagger.json")]
    [InlineData("/openapi/v1.json")]
    public async Task Enabled_production_swagger_rejects_an_authenticated_user_without_scope(string path)
    {
        await using var factory = new ProductionSwaggerFactory(enabled: true);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "integration-token");
        request.Headers.TryAddWithoutValidation("X-Test-Subject", "swagger-production-user");
        request.Headers.TryAddWithoutValidation("X-Test-Scope", "unrelated/scope");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}

public sealed class ProductionSwaggerFactory(bool? enabled, bool includeGatewaySecret = true) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var privateJwk = CreatePrivateJwk();
        builder.UseEnvironment("Production");
        if (includeGatewaySecret)
            builder.UseSetting("AuthGateway:SharedSecret", new string('g', 32));
        if (enabled.HasValue)
            builder.UseSetting("Swagger:EnabledInProduction", enabled.Value.ToString());
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var settings = new Dictionary<string, string?>
            {
                ["HelseId:Authority"] = "https://helseid-sts.test.nhn.no",
                ["HelseId:FacadeAudience"] = "nhn:population-data-facade",
                ["HelseId:FacadeScope"] = "nhn:population-data-facade/read",
                ["HelseId:DhgAudience"] = "nhn:maternity-record",
                ["HelseId:DhgScope"] = "nhn:maternity-record/api",
                ["HelseId:ClientId"] = "integration-client",
                ["HelseId:ClientAssertionJwk"] = privateJwk,
                ["HelseId:DPoPJwk"] = privateJwk
            };
            if (includeGatewaySecret)
                settings["AuthGateway:SharedSecret"] = new string('g', 32);
            if (enabled.HasValue)
                settings["Swagger:EnabledInProduction"] = enabled.Value.ToString();
            configuration.AddInMemoryCollection(settings);
        });
        builder.ConfigureTestServices(services => services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Integration";
                options.DefaultChallengeScheme = "Integration";
                options.DefaultScheme = "Integration";
            })
            .AddScheme<AuthenticationSchemeOptions, IntegrationAuthenticationHandler>("Integration", _ => { }));
    }

    internal static string CreatePrivateJwk()
    {
        using var rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa.ExportParameters(true)) { KeyId = Guid.NewGuid().ToString("N") };
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key);
        jwk.Alg = SecurityAlgorithms.RsaSsaPssSha256;
        return JsonSerializer.Serialize(jwk);
    }
}

public sealed class DhgProductionNonProductionHostFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var privateJwk = ProductionSwaggerFactory.CreatePrivateJwk();
        // Bruk en host som verken er Production eller Development, slik at developer user-secrets
        // ikke kan endre denne integration test av production boundary.
        builder.UseEnvironment("Testing");
        builder.UseSetting("Dhg:Environment", "Production");
        builder.UseSetting("AuthGateway:SharedSecret", new string('g', 32));
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Dhg:Environment"] = "Production",
                ["Dhg:BaseUrl"] = "https://maternity-record.nhn.no/api/maternity-record/v1/",
                ["HelseId:Authority"] = "https://helseid-sts.nhn.no",
                ["HelseId:FacadeAudience"] = "nhn:population-data-facade",
                ["HelseId:FacadeScope"] = "nhn:population-data-facade/read",
                ["HelseId:DhgAudience"] = "nhn:maternity-record",
                ["HelseId:DhgScope"] = "nhn:maternity-record/api",
                ["HelseId:ClientId"] = "integration-client",
                ["HelseId:ClientAssertionJwk"] = privateJwk,
                ["HelseId:DPoPJwk"] = privateJwk,
                ["AuthGateway:SharedSecret"] = new string('g', 32)
            }));
    }
}
