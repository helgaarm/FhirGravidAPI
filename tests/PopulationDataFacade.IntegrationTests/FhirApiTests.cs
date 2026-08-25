using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PopulationDataFacade.Core;
using Xunit;

namespace PopulationDataFacade.IntegrationTests;

public sealed class FhirApiTests(FhirFacadeFactory factory) : IClassFixture<FhirFacadeFactory>
{
    [Fact]
    public async Task Metadata_is_anonymous_and_fhir_json()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/fhir/metadata", TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/fhir+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("\"resourceType\":\"CapabilityStatement\"", json);
        Assert.Contains("\"name\":\"identifier\"", json);
        Assert.Contains("\"name\":\"patient.identifier\"", json);
        Assert.Contains("POST _search accepts NIN", json);

        using var document = JsonDocument.Parse(json);
        var observation = document.RootElement
            .GetProperty("rest")[0]
            .GetProperty("resource")
            .EnumerateArray()
            .Single(resource => resource.GetProperty("type").GetString() == "Observation");
        Assert.False(observation.TryGetProperty("profile", out _));
        Assert.False(observation.TryGetProperty("supportedProfile", out _));
        Assert.Contains(observation.GetProperty("searchParam").EnumerateArray(),
            parameter => parameter.GetProperty("name").GetString() == "category");
        Assert.Contains(observation.GetProperty("searchParam").EnumerateArray(),
            parameter => parameter.GetProperty("name").GetString() == "date");
        var careTeam = document.RootElement
            .GetProperty("rest")[0]
            .GetProperty("resource")
            .EnumerateArray()
            .Single(resource => resource.GetProperty("type").GetString() == "CareTeam");
        Assert.Contains(careTeam.GetProperty("searchParam").EnumerateArray(),
            parameter => parameter.GetProperty("name").GetString() == "patient");
        Assert.Contains(careTeam.GetProperty("searchParam").EnumerateArray(),
            parameter => parameter.GetProperty("name").GetString() == "patient.identifier");
    }

    [Fact]
    public async Task Metadata_uses_forwarded_https_scheme_behind_the_configured_proxy()
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/fhir/metadata");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "https://localhost/fhir/metadata",
            json.RootElement.GetProperty("url").GetString());
    }

    [Fact]
    public async Task Patient_read_uses_protected_context_and_never_returns_nin()
    {
        using var client = factory.CreateClient();
        var context = await IssueContextAsync(client);
        using var request = Authorized(HttpMethod.Get, $"/fhir/Patient/{context.PatientId}", context.Token);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"resourceType\":\"Patient\"", json);
        Assert.Contains("\"id\":\"patient-1\"", json);
        Assert.DoesNotContain("01019012345", json);
    }

    [Fact]
    public async Task Fetal_patient_is_resolvable_only_inside_the_maternal_protected_context()
    {
        using var client = factory.CreateClient();
        var context = await IssueContextAsync(client);
        using var request = Authorized(HttpMethod.Get, "/fhir/Patient/fetus-1", context.Token);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Patient", json.RootElement.GetProperty("resourceType").GetString());
        Assert.Equal("fetus-1", json.RootElement.GetProperty("id").GetString());
        Assert.False(json.RootElement.TryGetProperty("identifier", out _));
        Assert.False(json.RootElement.TryGetProperty("gender", out _));
        Assert.False(json.RootElement.TryGetProperty("birthDate", out _));
    }

    [Fact]
    public async Task Care_team_search_returns_the_marked_DHG_contact_fields()
    {
        using var client = factory.CreateClient();
        var context = await IssueContextAsync(client);
        using var request = Authorized(
            HttpMethod.Get,
            $"/fhir/CareTeam?patient={context.PatientId}",
            context.Token);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"resourceType\":\"CareTeam\"", json);
        Assert.Contains("\"resourceType\":\"PractitionerRole\"", json);
        Assert.Contains("#general-practitioner-role", json);
        Assert.Contains("Ola Fastlege", json);
        Assert.Contains("Kari Jordmor", json);
        Assert.Contains("Testsykehus", json);
        Assert.Contains("Sentrum helsestasjon", json);
        Assert.Contains(PopulationIdentifierSystems.HprNumber, json);
        Assert.Contains(PopulationIdentifierSystems.OrganizationNumber, json);
        Assert.Contains("1234567", json);
        Assert.Contains("7654321", json);
        Assert.Contains("994598759", json);
        Assert.DoesNotContain("01019012345", json);
    }

    [Fact]
    public async Task Unknown_code_returns_empty_search_bundle()
    {
        using var client = factory.CreateClient();
        var context = await IssueContextAsync(client);
        using var request = Authorized(
            HttpMethod.Get,
            $"/fhir/Observation?patient={context.PatientId}&code=urn%3Atest%7Cunknown",
            context.Token);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("searchset", json.RootElement.GetProperty("type").GetString());
        Assert.Equal(0, json.RootElement.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Observation_search_filters_category_and_date_without_profile_claims()
    {
        using var client = factory.CreateClient();
        var context = await IssueContextAsync(client);
        using var request = Authorized(
            HttpMethod.Get,
            $"/fhir/Observation?patient={context.PatientId}&category=vital-signs&date=ge2026-01-18",
            context.Token);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, json.RootElement.GetProperty("total").GetInt32());
        var observation = json.RootElement.GetProperty("entry")[0].GetProperty("resource");
        Assert.Equal("obs-weight", observation.GetProperty("id").GetString());
        Assert.False(observation.GetProperty("meta").TryGetProperty("profile", out _));
    }

    [Fact]
    public async Task Observation_search_rejects_an_invalid_date_filter()
    {
        using var client = factory.CreateClient();
        var context = await IssueContextAsync(client);
        using var request = Authorized(
            HttpMethod.Get,
            $"/fhir/Observation?patient={context.PatientId}&date=2026-99-99",
            context.Token);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/fhir+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Post_search_has_a_server_enforced_body_limit_and_rejects_chunked_oversize()
    {
        using var client = factory.CreateClient();
        var endpoint = factory.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(candidate => candidate.RoutePattern.RawText == "/fhir/Observation/_search");
        var sizeLimit = endpoint.Metadata.GetMetadata<IRequestSizeLimitMetadata>();
        Assert.NotNull(sizeLimit);
        Assert.Equal(4096, sizeLimit.MaxRequestBodySize);

        using var request = Authorized(HttpMethod.Post, "/fhir/Observation/_search");
        request.Headers.TransferEncodingChunked = true;
        request.Content = new StreamContent(new MemoryStream(
            Encoding.UTF8.GetBytes("patient.identifier=" + new string('1', 5000))));
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(
            "application/x-www-form-urlencoded");
        request.Content.Headers.ContentLength = null;

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/fhir+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Patient_context_cannot_be_replayed_for_another_logical_patient()
    {
        using var client = factory.CreateClient();
        var context = await IssueContextAsync(client);
        using var request = Authorized(HttpMethod.Get, "/fhir/Patient/another-patient", context.Token);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("\"resourceType\":\"OperationOutcome\"", json);
    }

    [Fact]
    public async Task Patient_context_cannot_be_replayed_by_another_authenticated_subject()
    {
        using var client = factory.CreateClient();
        var context = await IssueContextAsync(client);
        using var request = Authorized(
            HttpMethod.Get,
            $"/fhir/Patient/{context.PatientId}",
            context.Token,
            subject: "another-user");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        var json = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("\"resourceType\":\"OperationOutcome\"", json);
    }

    [Fact]
    public async Task Patient_endpoint_rejects_a_missing_access_token()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/fhir/Patient/patient-1",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/fhir+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Patient_endpoint_rejects_a_token_without_the_required_scope()
    {
        using var client = factory.CreateClient();
        using var request = Authorized(HttpMethod.Get, "/fhir/Patient/patient-1", scope: "other.scope");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/fhir+json", response.Content.Headers.ContentType?.MediaType);
    }

    private static async Task<(string PatientId, string Token)> IssueContextAsync(HttpClient client)
    {
        using var request = Authorized(HttpMethod.Post, "/test/patient-context/synthetic-1");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        return (
            json.RootElement.GetProperty("patientId").GetString()!,
            json.RootElement.GetProperty("patientContext").GetString()!);
    }

    private static HttpRequestMessage Authorized(
        HttpMethod method,
        string path,
        string? patientContext = null,
        string subject = "integration-user",
        string scope = "nhn:population-data-facade/read")
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "integration-subject-token");
        if (patientContext is not null) request.Headers.TryAddWithoutValidation("X-Patient-Context", patientContext);
        request.Headers.TryAddWithoutValidation("X-Test-Subject", subject);
        request.Headers.TryAddWithoutValidation("X-Test-Scope", scope);
        return request;
    }
}

public sealed class FhirFacadeFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var privateJwk = CreatePrivateJwk();
        builder.UseEnvironment("Testing");
        builder.UseSetting("AuthGateway:SharedSecret", new string('g', 32));
        builder.UseSetting("ReverseProxy:ForwardedHeadersEnabled", "true");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["HelseId:Authority"] = "https://helseid-sts.test.nhn.no",
                ["HelseId:FacadeAudience"] = "nhn:population-data-facade",
                ["HelseId:FacadeScope"] = "nhn:population-data-facade/read",
                ["HelseId:DhgAudience"] = "nhn:maternity-record",
                ["HelseId:DhgScope"] = "nhn:maternity-record/api",
                ["HelseId:ClientId"] = "integration-client",
                ["HelseId:ClientAssertionJwk"] = privateJwk,
                ["HelseId:DPoPJwk"] = privateJwk,
                ["AuthGateway:SharedSecret"] = new string('g', 32),
                ["PatientContext:PatientIdHmacKey"] = ProductionSwaggerFactory.PatientIdHmacKey,
                ["ReverseProxy:ForwardedHeadersEnabled"] = "true",
                ["PatientContext:TestAliases:synthetic-1:LogicalId"] = "patient-1",
                ["PatientContext:TestAliases:synthetic-1:NationalIdentityNumber"] = "01019012345"
            }));
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IStartupFilter, LoopbackRemoteAddressStartupFilter>();
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

    private static string CreatePrivateJwk()
    {
        using var rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa.ExportParameters(true)) { KeyId = Guid.NewGuid().ToString("N") };
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key);
        jwk.Alg = SecurityAlgorithms.RsaSsaPssSha256;
        return JsonSerializer.Serialize(jwk);
    }
}

public sealed class IntegrationAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
            return Task.FromResult(AuthenticateResult.NoResult());
        var subject = Request.Headers["X-Test-Subject"].FirstOrDefault() ?? "integration-user";
        var scope = Request.Headers["X-Test-Scope"].FirstOrDefault() ?? "nhn:population-data-facade/read";
        var identity = new ClaimsIdentity(
            [new Claim("sub", subject), new Claim("scope", scope)],
            Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}

public sealed class FixedPopulationDataService : IPopulationDataService
{
    public Task<PopulationSnapshot> GetSnapshotAsync(PatientRequestContext context, CancellationToken cancellationToken)
    {
        var updated = DateTimeOffset.Parse("2026-01-16T12:30:00+01:00");
        return Task.FromResult(new PopulationSnapshot(
            new PopulationPatient(context.LogicalId, new CodedValue("urn:ietf:bcp:47", "no", "Norsk"), false, updated),
            [
                new PopulationObservation("obs-1", PopulationCodes.Hemoglobin, new QuantityValue(12.4m, "g/dL", PopulationCodes.Ucum, "g/dL"), "laboratory", updated),
                new PopulationObservation(
                    "obs-weight",
                    PopulationCodes.MotherWeight,
                    new QuantityValue(68m, "kg", PopulationCodes.Ucum, "kg"),
                    "vital-signs",
                    updated,
                    new EffectiveDate(new DateOnly(2026, 1, 20)),
                    EncounterId: "encounter-1"),
                new PopulationObservation(
                    "obs-fetal-heart-rate",
                    PopulationCodes.FetalHeartRate,
                    new QuantityValue(145m, "slag/min", PopulationCodes.Ucum, "{beats}/min"),
                    "vital-signs",
                    updated,
                    new EffectiveDate(new DateOnly(2026, 1, 16)),
                    EncounterId: "encounter-1",
                    FocusPatientId: "fetus-1")
            ],
            [new PopulationEncounter("encounter-1", new DateOnly(2026, 1, 16), updated)],
            updated,
            true,
            [
                new PopulationCareTeam(
                    "care-team-1",
                    new PopulationCareTeamMember(
                        "Kari Jordmor",
                        "Sentrum jordmortjeneste",
                        "7654321"),
                    "Sentrum helsestasjon",
                    updated,
                    new PopulationCareTeamMember(
                        "Ola Fastlege",
                        "Sentrum legekontor",
                        "1234567",
                        "994598759"),
                    "Testsykehus")
            ],
            [new PopulationFetusPatient("fetus-1", updated)]));
    }
}
