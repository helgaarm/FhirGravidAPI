using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using PopulationDataFacade.Api.Security;
using PopulationDataFacade.Core;
using Xunit;

namespace PopulationDataFacade.IntegrationTests;

public sealed class DevelopmentTestModeTests
{
    [Theory]
    [InlineData("patient-1", true)]
    [InlineData("Patient.1", true)]
    [InlineData("patient_1", false)]
    [InlineData("patient/1", false)]
    [InlineData("", false)]
    public void Synthetic_logical_id_uses_the_FHIR_id_format(string logicalId, bool expected) =>
        Assert.Equal(expected, PatientContextOptions.IsLogicalIdFormat(logicalId));

    [Fact]
    public void Synthetic_logical_ids_are_unique_and_never_equal_a_configured_nin()
    {
        var duplicateIds = new Dictionary<string, SyntheticPatientOptions>
        {
            ["one"] = new() { LogicalId = "patient-1", NationalIdentityNumber = "01019012345" },
            ["two"] = new() { LogicalId = "patient-1", NationalIdentityNumber = "02029012345" }
        };
        var ninAsId = new Dictionary<string, SyntheticPatientOptions>
        {
            ["one"] = new() { LogicalId = "02029012345", NationalIdentityNumber = "01019012345" },
            ["two"] = new() { LogicalId = "patient-2", NationalIdentityNumber = "02029012345" }
        };

        Assert.False(PatientContextOptions.HaveUniqueLogicalIds(duplicateIds));
        Assert.False(PatientContextOptions.LogicalIdsDoNotContainNins(ninAsId));
    }

    [Theory]
    [InlineData("Testing", "Test")]
    [InlineData("Production", "Test")]
    [InlineData("Development", "Production")]
    [InlineData("Staging", "Test")]
    public void Anonymous_mode_is_rejected_without_a_supported_test_host_and_dhg_test(
        string hostEnvironment,
        string dhgEnvironment)
    {
        using var factory = new InvalidDevelopmentTestModeFactory(hostEnvironment, dhgEnvironment);

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("DevelopmentTestMode requires Dhg:Environment=Test", exception.ToString());
    }

    [Fact]
    public void Anonymous_mode_rejects_a_non_loopback_listener()
    {
        using var factory = new InvalidDevelopmentListenerFactory();

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("loopback-only listener", exception.ToString());
    }

    [Fact]
    public async Task Swagger_can_issue_context_and_read_fhir_without_inbound_helseid()
    {
        await using var factory = new DevelopmentTestModeFactory();
        using var client = factory.CreateClient();

        using var contextResponse = await client.PostAsync(
            "/test/patient-context/synthetic_1",
            null,
            TestContext.Current.CancellationToken);
        contextResponse.EnsureSuccessStatusCode();
        using var contextJson = JsonDocument.Parse(await contextResponse.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        var patientId = contextJson.RootElement.GetProperty("patientId").GetString();
        var patientContext = contextJson.RootElement.GetProperty("patientContext").GetString();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/fhir/Patient/{patientId}");
        request.Headers.TryAddWithoutValidation("X-Patient-Context", patientContext);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"resourceType\":\"Patient\"", json);
    }

    [Theory]
    [InlineData("/fhir/Patient/_search", "identifier", "Patient")]
    [InlineData("/fhir/Observation/_search", "patient.identifier", "Observation")]
    [InlineData("/fhir/Encounter/_search", "patient.identifier", "Encounter")]
    [InlineData("/fhir/CareTeam/_search", "patient.identifier", "CareTeam")]
    public async Task Local_post_search_resolves_only_the_configured_synthetic_nin_without_context(
        string path,
        string parameterName,
        string expectedResourceType)
    {
        await using var factory = new DevelopmentTestModeFactory();
        using var client = factory.CreateClient();
        using var content = new FormUrlEncodedContent(
            [new KeyValuePair<string, string>(parameterName, "01019012345")]);

        using var response = await client.PostAsync(path, content, TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/fhir+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("\"resourceType\":\"Bundle\"", json);
        Assert.Contains($"\"resourceType\":\"{expectedResourceType}\"", json);
        Assert.Contains("patient-1", json);
        Assert.DoesNotContain("01019012345", json);
    }

    [Fact]
    public async Task Local_post_search_rejects_an_unconfigured_nin_without_echoing_it()
    {
        const string unconfiguredSyntheticNin = "11111111111";
        await using var factory = new DevelopmentTestModeFactory();
        using var client = factory.CreateClient();
        using var content = new FormUrlEncodedContent(
            [new KeyValuePair<string, string>("identifier", unconfiguredSyntheticNin)]);

        using var response = await client.PostAsync(
            "/fhir/Patient/_search",
            content,
            TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("\"resourceType\":\"OperationOutcome\"", json);
        Assert.DoesNotContain(unconfiguredSyntheticNin, json);
    }

    [Fact]
    public async Task Local_observation_post_search_supports_the_existing_code_filter()
    {
        await using var factory = new DevelopmentTestModeFactory();
        using var client = factory.CreateClient();
        using var content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("patient.identifier", "01019012345"),
            new KeyValuePair<string, string>("code", "urn:test|unknown")
        ]);

        using var response = await client.PostAsync(
            "/fhir/Observation/_search",
            content,
            TestContext.Current.CancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, json.RootElement.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Local_observation_post_search_supports_category_and_date_filters()
    {
        await using var factory = new DevelopmentTestModeFactory();
        using var client = factory.CreateClient();
        using var content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("patient.identifier", "01019012345"),
            new KeyValuePair<string, string>("category", "vital-signs"),
            new KeyValuePair<string, string>("date", "ge2026-01-18")
        ]);

        using var response = await client.PostAsync(
            "/fhir/Observation/_search",
            content,
            TestContext.Current.CancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, json.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(
            "obs-weight",
            json.RootElement.GetProperty("entry")[0].GetProperty("resource").GetProperty("id").GetString());
    }

    [Fact]
    public async Task HelseId_TEST_token_mode_starts_without_private_JWKs()
    {
        await using var factory = new DevelopmentTestModeFactory(useTestTokenUtility: true);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/swagger/v1/swagger.json",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_documents_the_patient_context_header()
    {
        await using var factory = new DevelopmentTestModeFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/swagger/v1/swagger.json",
            TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"name\": \"X-Patient-Context\"", json);
        Assert.Contains("/fhir/Patient/{id}", json);
    }

    [Fact]
    public async Task Swagger_documents_local_nin_search_as_form_body_without_context_header()
    {
        await using var factory = new DevelopmentTestModeFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/swagger/v1/swagger.json",
            TestContext.Current.CancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        var operation = json.RootElement
            .GetProperty("paths")
            .GetProperty("/fhir/Observation/_search")
            .GetProperty("post");
        var schema = operation
            .GetProperty("requestBody")
            .GetProperty("content")
            .GetProperty("application/x-www-form-urlencoded")
            .GetProperty("schema");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(schema.GetProperty("properties").TryGetProperty("patient.identifier", out _));
        Assert.True(schema.GetProperty("properties").TryGetProperty("code", out _));
        Assert.True(schema.GetProperty("properties").TryGetProperty("category", out _));
        Assert.True(schema.GetProperty("properties").TryGetProperty("date", out _));
        Assert.DoesNotContain("X-Patient-Context", operation.ToString());
    }

    [Fact]
    public async Task Local_capability_statement_advertises_patient_identifier_search()
    {
        await using var factory = new DevelopmentTestModeFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/fhir/metadata",
            TestContext.Current.CancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        var patient = json.RootElement
            .GetProperty("rest")[0]
            .GetProperty("resource")
            .EnumerateArray()
            .Single(resource => resource.GetProperty("type").GetString() == "Patient");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(
            patient.GetProperty("interaction").EnumerateArray(),
            interaction => interaction.GetProperty("code").GetString() == "search-type");
        Assert.Contains(
            patient.GetProperty("searchParam").EnumerateArray(),
            parameter => parameter.GetProperty("name").GetString() == "identifier");
    }

    [Fact]
    public async Task Anonymous_mode_rejects_non_loopback_requests()
    {
        await using var factory = new DevelopmentTestModeFactory();
        _ = factory.CreateClient();

        var context = await factory.Server.SendAsync(request =>
            {
                request.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.1");
                request.Request.Method = HttpMethod.Get.Method;
                request.Request.Path = "/swagger/v1/swagger.json";
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_mode_rejects_requests_with_an_unknown_remote_address()
    {
        await using var factory = new DevelopmentTestModeFactory(simulateLoopback: false);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/swagger/v1/swagger.json",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}

public sealed class InvalidDevelopmentTestModeFactory(
    string hostEnvironment,
    string dhgEnvironment) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(hostEnvironment);
        builder.UseSetting("DevelopmentTestMode:Enabled", "true");
        builder.UseSetting("Dhg:Environment", dhgEnvironment);
    }
}

public sealed class InvalidDevelopmentListenerFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("DevelopmentTestMode:Enabled", "true");
        builder.UseSetting("Dhg:Environment", "Test");
        builder.UseSetting("urls", "http://0.0.0.0:5000");
    }
}

public sealed class DevelopmentTestModeFactory(
    bool simulateLoopback = true,
    bool useTestTokenUtility = false) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var privateJwk = CreatePrivateJwk();
        builder.UseEnvironment("Development");
        builder.UseSetting("DevelopmentTestMode:Enabled", "true");
        builder.UseSetting("DevelopmentTestMode:Subject", "swagger-integration-user");
        builder.UseSetting("Dhg:Environment", "Test");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["DevelopmentTestMode:Enabled"] = "true",
                ["DevelopmentTestMode:Subject"] = "swagger-integration-user",
                ["Dhg:Environment"] = "Test",
                ["HelseId:Authority"] = "https://helseid-sts.test.nhn.no",
                ["HelseId:FacadeAudience"] = "nhn:population-data-facade",
                ["HelseId:FacadeScope"] = "nhn:population-data-facade/read",
                ["HelseId:DhgAudience"] = "nhn:maternity-record",
                ["HelseId:DhgScope"] = "nhn:maternity-record/api",
                ["HelseId:ClientId"] = "integration-client",
                ["HelseId:ClientAssertionJwk"] = useTestTokenUtility ? string.Empty : privateJwk,
                ["HelseId:DPoPJwk"] = useTestTokenUtility ? string.Empty : privateJwk,
                ["HelseIdTestToken:Enabled"] = useTestTokenUtility.ToString(),
                ["HelseIdTestToken:AuthKey"] = useTestTokenUtility ? "integration-test-auth-key" : string.Empty,
                ["HelseIdTestToken:OrgnrParent"] = useTestTokenUtility ? "123456789" : string.Empty,
                ["HelseIdTestToken:OrgnrChild"] = useTestTokenUtility ? "987654321" : string.Empty,
                ["HelseIdTestToken:ClientTenancyType"] = "1",
                ["HelseIdTestToken:PractitionerNationalIdentityNumber"] = useTestTokenUtility ? "06828399789" : string.Empty,
                ["HelseIdTestToken:PractitionerHprNumber"] = useTestTokenUtility ? "565505933" : string.Empty,
                ["HelseIdTestToken:PractitionerName"] = useTestTokenUtility ? "KVART GREVLING" : string.Empty,
                ["HelseIdTestToken:UserRoleCode"] = useTestTokenUtility ? "LE" : string.Empty,
                ["HelseIdTestToken:TreatmentFacilityName"] = useTestTokenUtility ? "Test facility" : string.Empty,
                ["PatientContext:TestAliases:synthetic_1:LogicalId"] = "patient-1",
                ["PatientContext:TestAliases:synthetic_1:NationalIdentityNumber"] = "01019012345"
            }));
        builder.ConfigureTestServices(services =>
        {
            if (simulateLoopback)
                services.AddSingleton<IStartupFilter, LoopbackRemoteAddressStartupFilter>();
            services.RemoveAll<IPopulationDataService>();
            services.AddSingleton<IPopulationDataService, FixedPopulationDataService>();
        });
    }

    private static string CreatePrivateJwk()
    {
        using var rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa.ExportParameters(true)) { KeyId = Guid.NewGuid().ToString("N") };
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key);
        jwk.Alg = SecurityAlgorithms.RsaSsaPssSha256;
        return JsonSerializer.Serialize(jwk);
    }
}

public sealed class LoopbackRemoteAddressStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => application =>
    {
        application.Use(async (context, nextMiddleware) =>
        {
            context.Connection.RemoteIpAddress ??= IPAddress.Loopback;
            await nextMiddleware();
        });
        next(application);
    };
}
