using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PopulationDataFacade.Core;
using PopulationDataFacade.Infrastructure;
using PopulationDataFacade.Infrastructure.Configuration;
using PopulationDataFacade.Infrastructure.Dhg;
using PopulationDataFacade.Infrastructure.HelseId;
using Xunit;

namespace PopulationDataFacade.Tests;

public sealed class HelseIdTestTokenAuthorizationProviderTests
{
    [Fact]
    public async Task Provider_requests_a_fresh_request_bound_token_and_proof_for_each_DHG_call()
    {
        var handler = new TestTokenHandler(call => Json(HttpStatusCode.OK, $$"""
            {
              "successResponse": {
                "accessTokenJwt": "access-token-{{call}}",
                "dPoPProof": "proof-{{call}}"
              }
            }
            """));
        var provider = Provider(handler);
        var destination = new Uri(
            "https://maternity-record.hit.test.nhn.no/api/maternity-record/v1/status?include=current");

        var first = await provider.AuthorizeAsync(
            string.Empty, HttpMethod.Get, destination, null, CancellationToken.None);
        var second = await provider.AuthorizeAsync(
            string.Empty, HttpMethod.Get, destination, null, CancellationToken.None);

        Assert.Equal("access-token-1", first.AccessToken);
        Assert.Equal("proof-1", first.DPoPProof);
        Assert.Equal("access-token-2", second.AccessToken);
        Assert.Equal("proof-2", second.DPoPProof);
        Assert.Null(first.UserRoleHeader);
        Assert.Null(first.TreatmentFacilityNameHeader);
        Assert.Equal(2, handler.Requests.Count);

        var request = handler.Requests[0];
        Assert.Equal("https://helseid-ttt.test.nhn.no/v2/create-test-token-with-key", request.Uri.ToString());
        Assert.Equal("test-auth-key", request.AuthKey);
        Assert.DoesNotContain("test-auth-key", request.Body, StringComparison.Ordinal);

        using var payload = JsonDocument.Parse(request.Body);
        var root = payload.RootElement;
        Assert.Equal("nhn:maternity-record", root.GetProperty("audience").GetString());
        Assert.Equal(0, root.GetProperty("issuerEnvironment").GetInt32());
        Assert.True(root.GetProperty("createDPoPTokenWithDPoPProof").GetBoolean());
        Assert.Equal("GET", root.GetProperty("dPoPProofParameters").GetProperty("htmClaimValue").GetString());
        Assert.Equal(destination.ToString(),
            root.GetProperty("dPoPProofParameters").GetProperty("htuClaimValue").GetString());

        var claims = root.GetProperty("clientClaimsParameters");
        Assert.Equal("dhg-test-client", claims.GetProperty("clientId").GetString());
        Assert.Equal("123456789", claims.GetProperty("orgnrParent").GetString());
        Assert.Equal("987654321", claims.GetProperty("orgnrChild").GetString());
        Assert.True(claims.GetProperty("clientTenancy").GetBoolean());
        Assert.Equal(1, claims.GetProperty("clientTenancyType").GetInt32());
        Assert.Equal("private_key_jwt", claims.GetProperty("clientAuthenticationMethodsReferences").GetString());
        var scopes = claims.GetProperty("scope");
        Assert.Equal(JsonValueKind.Array, scopes.ValueKind);
        Assert.Equal("nhn:maternity-record/api", Assert.Single(scopes.EnumerateArray()).GetString());
        Assert.False(root.TryGetProperty("userClaimsParameters", out _));
    }

    [Fact]
    public async Task Provider_requests_user_claims_and_DHG_audit_headers_for_record_calls()
    {
        var handler = new TestTokenHandler(_ => Json(HttpStatusCode.OK, """
            {
              "successResponse": {
                "accessTokenJwt": "access-token",
                "dPoPProof": "proof"
              }
            }
            """));
        var provider = Provider(handler);
        var destination = new Uri(
            "https://maternity-record.hit.test.nhn.no/api/maternity-record/v1/record/3d2d76b2-7675-46ff-a4d7-2ed3e0cb80f8");

        var authorization = await provider.AuthorizeAsync(
            string.Empty, HttpMethod.Get, destination, null, CancellationToken.None);

        using var payload = JsonDocument.Parse(Assert.Single(handler.Requests).Body);
        var userClaims = payload.RootElement.GetProperty("userClaimsParameters");
        Assert.Equal("06828399789", userClaims.GetProperty("pid").GetString());
        Assert.Equal("565505933", userClaims.GetProperty("hprNumber").GetString());
        Assert.Equal("KVART GREVLING", userClaims.GetProperty("name").GetString());
        Assert.Equal("4", userClaims.GetProperty("securityLevel").GetString());

        var role = Uri.UnescapeDataString(authorization.UserRoleHeader!);
        using var roleJson = JsonDocument.Parse(role);
        Assert.Equal("urn:oid:2.16.578.1.12.4.1.1.9060", roleJson.RootElement.GetProperty("system").GetString());
        Assert.Equal("LE", roleJson.RootElement.GetProperty("code").GetString());
        Assert.Equal("Loddefjord legesenter", Uri.UnescapeDataString(authorization.TreatmentFacilityNameHeader!));
    }

    [Fact]
    public async Task Provider_does_not_expose_utility_response_or_auth_key_on_rejection()
    {
        var handler = new TestTokenHandler(_ => Json(
            HttpStatusCode.Unauthorized,
            "{\"detail\":\"accessTokenJwt=leaked-token; auth=server-secret\"}"));
        var provider = Provider(handler, "server-secret");

        var exception = await Assert.ThrowsAsync<PopulationDataException>(() => provider.AuthorizeAsync(
            string.Empty,
            HttpMethod.Get,
            new Uri("https://maternity-record.hit.test.nhn.no/api/maternity-record/v1/status"),
            null,
            CancellationToken.None));

        Assert.Equal(PopulationErrorKind.ConfigurationInvalid, exception.Kind);
        Assert.DoesNotContain("leaked-token", exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("server-secret", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Provider_rejects_a_DHG_nonce_it_cannot_bind_without_calling_the_utility()
    {
        var handler = new TestTokenHandler(_ => Json(HttpStatusCode.OK, "{}"));
        var provider = Provider(handler);

        var exception = await Assert.ThrowsAsync<PopulationDataException>(() => provider.AuthorizeAsync(
            string.Empty,
            HttpMethod.Get,
            new Uri("https://maternity-record.hit.test.nhn.no/api/maternity-record/v1/status"),
            "dhg-nonce",
            CancellationToken.None));

        Assert.Equal(PopulationErrorKind.SourceUnavailable, exception.Kind);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public void Test_token_configuration_is_allowed_only_in_explicit_DHG_test_mode()
    {
        var options = TestTokenOptions();

        Assert.True(new HelseIdTestTokenOptionsValidator(true, "Test").Validate(null, options).Succeeded);
        Assert.True(new HelseIdTestTokenOptionsValidator(false, "Test").Validate(null, options).Failed);
        Assert.True(new HelseIdTestTokenOptionsValidator(true, "Production").Validate(null, options).Failed);

        options.Endpoint = new Uri("https://example.test.invalid/token");
        Assert.True(new HelseIdTestTokenOptionsValidator(true, "Test").Validate(null, options).Failed);
    }

    [Fact]
    public void Test_token_mode_does_not_require_private_JWKs_but_still_requires_the_registered_client_id()
    {
        var validator = new HelseIdOptionsValidator(useTestTokenUtility: true);

        Assert.True(validator.Validate(null, new HelseIdOptions { ClientId = "dhg-test-client" }).Succeeded);
        Assert.True(validator.Validate(null, new HelseIdOptions()).Failed);
    }

    [Fact]
    public void Dependency_injection_selects_the_test_token_provider_only_when_both_test_switches_are_enabled()
    {
        var values = new Dictionary<string, string?>
        {
            ["Dhg:Environment"] = "Test",
            ["DevelopmentTestMode:Enabled"] = "true",
            ["HelseIdTestToken:Enabled"] = "true",
            ["HelseIdTestToken:AuthKey"] = "test-auth-key",
            ["HelseIdTestToken:OrgnrParent"] = "123456789",
            ["HelseIdTestToken:OrgnrChild"] = "987654321",
            ["HelseIdTestToken:PractitionerNationalIdentityNumber"] = "06828399789",
            ["HelseIdTestToken:PractitionerHprNumber"] = "565505933",
            ["HelseIdTestToken:PractitionerName"] = "KVART GREVLING",
            ["HelseIdTestToken:UserRoleCode"] = "LE",
            ["HelseIdTestToken:TreatmentFacilityName"] = "Loddefjord legesenter",
            ["HelseId:ClientId"] = "dhg-test-client"
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPopulationDataFacadeInfrastructure(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        Assert.IsType<HelseIdTestTokenAuthorizationProvider>(
            scope.ServiceProvider.GetRequiredService<IDhgAuthorizationProvider>());
    }

    private static HelseIdTestTokenAuthorizationProvider Provider(
        HttpMessageHandler handler,
        string authKey = "test-auth-key") => new(
        new TestHttpClientFactory(handler),
        Options.Create(TestTokenOptions(authKey)),
        Options.Create(new HelseIdOptions { ClientId = "dhg-test-client" }),
        Options.Create(new DhgOptions()));

    private static HelseIdTestTokenOptions TestTokenOptions(string authKey = "test-auth-key") => new()
    {
        Enabled = true,
        AuthKey = authKey,
        OrgnrParent = "123456789",
        OrgnrChild = "987654321",
        ClientTenancyType = 1,
        PractitionerNationalIdentityNumber = "06828399789",
        PractitionerHprNumber = "565505933",
        PractitionerName = "KVART GREVLING",
        UserRoleCode = "LE",
        TreatmentFacilityName = "Loddefjord legesenter"
    };

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string json) => new(statusCode)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };

    private sealed class TestHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            Assert.Equal("HelseIdTestToken", name);
            return new HttpClient(handler, disposeHandler: false);
        }
    }

    private sealed record RecordedTestTokenRequest(Uri Uri, string? AuthKey, string Body);

    private sealed class TestTokenHandler(Func<int, HttpResponseMessage> response) : HttpMessageHandler
    {
        private int callCount;
        public List<RecordedTestTokenRequest> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Requests.Add(new RecordedTestTokenRequest(
                request.RequestUri!,
                request.Headers.TryGetValues("x-auth-key", out var values) ? values.SingleOrDefault() : null,
                body));
            return response(Interlocked.Increment(ref callCount));
        }
    }
}
