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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using PopulationDataFacade.Core;
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

    [Fact]
    public void Production_startup_requires_the_patient_id_hmac_key()
    {
        using var factory = new ProductionSwaggerFactory(
            enabled: null,
            includePatientIdHmacKey: false);

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("PatientContext:PatientIdHmacKey must be a Base64-encoded secret", exception.ToString());
    }

    [Theory]
    [InlineData("not-base64")]
    [InlineData("c2hvcnQ=")]
    public void Production_startup_rejects_an_invalid_patient_id_hmac_key(string invalidKey)
    {
        using var factory = new ProductionSwaggerFactory(
            enabled: null,
            patientIdHmacKey: invalidKey);

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("PatientContext:PatientIdHmacKey must be a Base64-encoded secret", exception.ToString());
    }

    [Theory]
    [InlineData("/fhir/Patient/_search", "identifier")]
    [InlineData("/fhir/Observation/_search", "patient.identifier")]
    [InlineData("/fhir/Encounter/_search", "patient.identifier")]
    [InlineData("/fhir/CareTeam/_search", "patient.identifier")]
    public async Task Production_post_search_requires_helseid(string path, string parameter)
    {
        await using var factory = new ProductionSwaggerFactory(enabled: null);
        using var client = factory.CreateClient();
        using var content = new FormUrlEncodedContent(
            [new KeyValuePair<string, string>(parameter, "01019012345")]);

        using var response = await client.PostAsync(path, content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/fhir/Patient/_search", "identifier", "Patient")]
    [InlineData("/fhir/Observation/_search", "patient.identifier", "Observation")]
    [InlineData("/fhir/Encounter/_search", "patient.identifier", "Encounter")]
    [InlineData("/fhir/CareTeam/_search", "patient.identifier", "CareTeam")]
    public async Task Production_post_search_accepts_helseid_without_patient_context(
        string path,
        string parameter,
        string resourceType)
    {
        const string nationalIdentityNumber = "01019012345";
        await using var factory = new ProductionSwaggerFactory(enabled: null);
        using var client = factory.CreateClient();
        using var request = AuthorizedPost(path, parameter, nationalIdentityNumber);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"\"resourceType\":\"{resourceType}\"", body);
        Assert.DoesNotContain(nationalIdentityNumber, body);
    }

    [Theory]
    [InlineData("01019012345")]
    [InlineData("00000000001")]
    public async Task Production_post_search_returns_a_stable_fhir_safe_hmac_pseudonymous_patient_id(
        string nationalIdentityNumber)
    {
        await using var factory = new ProductionSwaggerFactory(enabled: null);
        using var client = factory.CreateClient();

        var firstId = await SearchPatientIdAsync(client, nationalIdentityNumber);
        var secondId = await SearchPatientIdAsync(client, nationalIdentityNumber);

        Assert.Equal(firstId, secondId);
        Assert.Matches("^patient-[A-Za-z0-9.-]{43}$", firstId);
        Assert.DoesNotContain(nationalIdentityNumber, firstId);
    }

    [Fact]
    public async Task Production_post_search_rejects_helseid_without_population_scope()
    {
        await using var factory = new ProductionSwaggerFactory(enabled: null);
        using var client = factory.CreateClient();
        using var request = AuthorizedPost(
            "/fhir/Patient/_search",
            "identifier",
            "01019012345",
            scope: "unrelated/scope");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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

    private static HttpRequestMessage AuthorizedPost(
        string path,
        string parameter,
        string nationalIdentityNumber,
        string scope = "nhn:population-data-facade/read")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new FormUrlEncodedContent(
                [new KeyValuePair<string, string>(parameter, nationalIdentityNumber)])
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "integration-subject-token");
        request.Headers.TryAddWithoutValidation("X-Test-Subject", "production-search-user");
        request.Headers.TryAddWithoutValidation("X-Test-Scope", scope);
        return request;
    }

    private static async Task<string> SearchPatientIdAsync(HttpClient client, string nationalIdentityNumber)
    {
        using var request = AuthorizedPost(
            "/fhir/Patient/_search",
            "identifier",
            nationalIdentityNumber);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        return json.RootElement
            .GetProperty("entry")[0]
            .GetProperty("resource")
            .GetProperty("id")
            .GetString()!;
    }
}

public sealed class ProductionSwaggerFactory(
    bool? enabled,
    bool includeGatewaySecret = true,
    bool includePatientIdHmacKey = true,
    string? patientIdHmacKey = null) : WebApplicationFactory<Program>
{
    internal static readonly string PatientIdHmacKey = Convert.ToBase64String(
        Enumerable.Repeat((byte)0x42, 32).ToArray());

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var privateJwk = CreatePrivateJwk();
        var configuredPatientIdHmacKey = patientIdHmacKey ?? PatientIdHmacKey;
        builder.UseEnvironment("Production");
        if (includeGatewaySecret)
            builder.UseSetting("AuthGateway:SharedSecret", new string('g', 32));
        if (includePatientIdHmacKey)
            builder.UseSetting("PatientContext:PatientIdHmacKey", configuredPatientIdHmacKey);
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
            if (includePatientIdHmacKey)
                settings["PatientContext:PatientIdHmacKey"] = configuredPatientIdHmacKey;
            if (enabled.HasValue)
                settings["Swagger:EnabledInProduction"] = enabled.Value.ToString();
            configuration.AddInMemoryCollection(settings);
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPopulationDataService>();
            services.AddSingleton<IPopulationDataService, FixedPopulationDataService>();
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "Integration";
                options.DefaultChallengeScheme = "Integration";
                options.DefaultScheme = "Integration";
            })
            .AddScheme<AuthenticationSchemeOptions, IntegrationAuthenticationHandler>("Integration", _ => { });
        });
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
        builder.UseSetting("PatientContext:PatientIdHmacKey", ProductionSwaggerFactory.PatientIdHmacKey);
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
                ["AuthGateway:SharedSecret"] = new string('g', 32),
                ["PatientContext:PatientIdHmacKey"] = ProductionSwaggerFactory.PatientIdHmacKey
            }));
    }
}
