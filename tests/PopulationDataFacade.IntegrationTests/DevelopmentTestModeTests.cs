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
using PopulationDataFacade.Core;
using Xunit;

namespace PopulationDataFacade.IntegrationTests;

public sealed class DevelopmentTestModeTests
{
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

        using var contextResponse = await client.PostAsync("/test/patient-context/synthetic-1", null);
        contextResponse.EnsureSuccessStatusCode();
        using var contextJson = JsonDocument.Parse(await contextResponse.Content.ReadAsStringAsync());
        var patientId = contextJson.RootElement.GetProperty("patientId").GetString();
        var patientContext = contextJson.RootElement.GetProperty("patientContext").GetString();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/fhir/Patient/{patientId}");
        request.Headers.TryAddWithoutValidation("X-Patient-Context", patientContext);
        using var response = await client.SendAsync(request);
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"resourceType\":\"Patient\"", json);
    }

    [Fact]
    public async Task Explicit_remote_staging_mode_accepts_non_loopback_requests()
    {
        await using var factory = new DevelopmentTestModeFactory(
            simulateLoopback: false,
            hostEnvironment: "Staging",
            allowRemoteStaging: true);
        using var client = factory.CreateClient();

        using var contextResponse = await client.PostAsync("/test/patient-context/synthetic-1", null);
        contextResponse.EnsureSuccessStatusCode();
        using var contextJson = JsonDocument.Parse(await contextResponse.Content.ReadAsStringAsync());
        var patientId = contextJson.RootElement.GetProperty("patientId").GetString();
        var patientContext = contextJson.RootElement.GetProperty("patientContext").GetString();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/fhir/Patient/{patientId}");
        request.Headers.TryAddWithoutValidation("X-Patient-Context", patientContext);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void Remote_staging_flag_cannot_enable_test_mode_in_production()
    {
        using var factory = new InvalidDevelopmentTestModeFactory(
            "Production",
            "Test",
            allowRemoteStaging: true);

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("DevelopmentTestMode requires Dhg:Environment=Test", exception.ToString());
    }

    [Fact]
    public async Task Swagger_documents_the_patient_context_header()
    {
        await using var factory = new DevelopmentTestModeFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/swagger/v1/swagger.json");
        var json = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"name\": \"X-Patient-Context\"", json);
        Assert.Contains("/fhir/Patient/{id}", json);
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
        });

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_mode_rejects_requests_with_an_unknown_remote_address()
    {
        await using var factory = new DevelopmentTestModeFactory(simulateLoopback: false);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}

public sealed class InvalidDevelopmentTestModeFactory(
    string hostEnvironment,
    string dhgEnvironment,
    bool allowRemoteStaging = false) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(hostEnvironment);
        builder.UseSetting("DevelopmentTestMode:Enabled", "true");
        builder.UseSetting("DevelopmentTestMode:AllowRemoteStaging", allowRemoteStaging.ToString());
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
    string hostEnvironment = "Development",
    bool allowRemoteStaging = false) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var privateJwk = CreatePrivateJwk();
        builder.UseEnvironment(hostEnvironment);
        builder.UseSetting("DevelopmentTestMode:Enabled", "true");
        builder.UseSetting("DevelopmentTestMode:AllowRemoteStaging", allowRemoteStaging.ToString());
        builder.UseSetting("DevelopmentTestMode:Subject", "swagger-integration-user");
        builder.UseSetting("Dhg:Environment", "Test");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["DevelopmentTestMode:Enabled"] = "true",
                ["DevelopmentTestMode:AllowRemoteStaging"] = allowRemoteStaging.ToString(),
                ["DevelopmentTestMode:Subject"] = "swagger-integration-user",
                ["Dhg:Environment"] = "Test",
                ["HelseId:Authority"] = "https://helseid-sts.test.nhn.no",
                ["HelseId:FacadeAudience"] = "nhn:population-data-facade",
                ["HelseId:FacadeScope"] = "nhn:population-data-facade/read",
                ["HelseId:DhgAudience"] = "nhn:maternity-record",
                ["HelseId:DhgScope"] = "nhn:maternity-record/api",
                ["HelseId:ClientId"] = "integration-client",
                ["HelseId:ClientAssertionJwk"] = privateJwk,
                ["HelseId:DPoPJwk"] = privateJwk,
                ["PatientContext:TestAliases:synthetic-1:LogicalId"] = "patient-1",
                ["PatientContext:TestAliases:synthetic-1:NationalIdentityNumber"] = "01019012345"
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
