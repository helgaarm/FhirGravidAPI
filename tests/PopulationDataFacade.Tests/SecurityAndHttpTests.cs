using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Duende.AccessTokenManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PopulationDataFacade.Core;
using PopulationDataFacade.Infrastructure.Configuration;
using PopulationDataFacade.Infrastructure.Dhg;
using PopulationDataFacade.Infrastructure.HelseId;
using Xunit;

namespace PopulationDataFacade.Tests;

public sealed class SecurityAndHttpTests
{
    [Fact]
    public void Client_assertion_matches_current_HelseId_profile()
    {
        var now = DateTimeOffset.Parse("2026-08-19T00:00:00Z");
        var options = Options.Create(new HelseIdOptions
        {
            ClientId = "actor-client",
            ClientAssertionJwk = PrivateJwk()
        });
        var factory = new HelseIdClientAssertionFactory(options, new FixedTimeProvider(now));

        var assertion = factory.Create(new Uri("https://helseid-sts.test.nhn.no"));
        var jwt = new JsonWebToken(assertion.Value);

        Assert.Equal("urn:ietf:params:oauth:client-assertion-type:jwt-bearer", assertion.Type);
        Assert.Equal("client-authentication+jwt", jwt.Typ);
        Assert.Equal("actor-client", jwt.Issuer);
        Assert.Equal("actor-client", jwt.Subject);
        Assert.Contains("https://helseid-sts.test.nhn.no", jwt.Audiences);
        Assert.True(jwt.ValidTo - jwt.ValidFrom <= TimeSpan.FromSeconds(8));
        Assert.True(jwt.TryGetClaim("jti", out var jti));
        Assert.False(string.IsNullOrWhiteSpace(jti.Value));
    }

    [Fact]
    public async Task Dhg_client_sends_sensitive_identifier_only_in_required_header()
    {
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK,
            "{\"hasGivenConsent\":true,\"hasActiveMaternityRecord\":true,\"deceased\":false}"));
        var authorization = new RecordingAuthorizationProvider();
        var client = Client(handler, authorization);

        await client.GetStatusAsync(Context(), CancellationToken.None);

        var request = Assert.Single(handler.Requests);
        Assert.DoesNotContain("01019012345", request.Uri.ToString(), StringComparison.Ordinal);
        Assert.Equal("01019012345", request.PatientNin);
        Assert.Equal("DPoP", request.AuthorizationScheme);
        Assert.Equal("source/1.0", request.SourceSystem);
    }

    [Fact]
    public async Task Dhg_client_retries_one_resource_nonce_challenge_with_new_proof()
    {
        var call = 0;
        var handler = new RecordingHandler(_ =>
        {
            call++;
            if (call == 1)
            {
                var challenge = Json(HttpStatusCode.Unauthorized, "{}");
                challenge.Headers.TryAddWithoutValidation("DPoP-Nonce", "resource-nonce");
                return challenge;
            }
            return Json(HttpStatusCode.OK,
                "{\"hasGivenConsent\":true,\"hasActiveMaternityRecord\":true,\"deceased\":false}");
        });
        var authorization = new RecordingAuthorizationProvider();
        var client = Client(handler, authorization);

        await client.GetStatusAsync(Context(), CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal([null, "resource-nonce"], authorization.Nonces);
    }

    [Fact]
    public async Task Dhg_client_retries_a_timeout_without_masking_caller_cancellation()
    {
        var call = 0;
        var handler = new RecordingHandler(_ =>
        {
            call++;
            if (call == 1) throw new TaskCanceledException("simulated client timeout");
            return Json(HttpStatusCode.OK,
                "{\"hasGivenConsent\":true,\"hasActiveMaternityRecord\":true,\"deceased\":false}");
        });
        var client = Client(handler, new RecordingAuthorizationProvider(), maxTransientRetries: 1);

        await client.GetStatusAsync(Context(), CancellationToken.None);

        Assert.Equal(2, call);
    }

    [Fact]
    public void Retry_after_http_date_is_respected()
    {
        var now = DateTimeOffset.Parse("2026-08-19T00:00:00Z");
        var retryAfter = new RetryConditionHeaderValue(now.AddSeconds(7));

        var delay = DhgHttpClient.CalculateRetryDelay(0, retryAfter, now);

        Assert.Equal(TimeSpan.FromSeconds(7), delay);
    }

    [Fact]
    public async Task Dhg_activity_never_contains_the_record_identifier()
    {
        const string recordId = "0f0b2f66-34f2-490b-a089-aaa6aa4c9825";
        var stopped = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "PopulationDataFacade.Dhg",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stopped.Add
        };
        ActivitySource.AddActivityListener(listener);
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.OK, "{}"));
        var client = Client(handler, new RecordingAuthorizationProvider());

        await client.GetRecordAsync(recordId, Context(), CancellationToken.None);

        var activity = Assert.Single(stopped);
        var exportedTags = string.Join("|", activity.TagObjects.Select(tag => $"{tag.Key}={tag.Value}"));
        Assert.Equal("record", activity.GetTagItem("dhg.operation"));
        Assert.DoesNotContain(recordId, exportedTags, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("QA")]
    [InlineData("Prodution")]
    public void Dhg_environment_rejects_unsupported_values(string environment)
    {
        var validator = new DhgOptionsValidator(Options.Create(new HelseIdOptions
        {
            Authority = new Uri("https://helseid-sts.test.nhn.no")
        }));

        var result = validator.Validate(null, new DhgOptions { Environment = environment });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("must be Test or Production", StringComparison.Ordinal));
    }

    [Fact]
    public void HelseId_configuration_requires_the_facade_scope()
    {
        var key = PrivateJwk();
        var result = new HelseIdOptionsValidator().Validate(null, new HelseIdOptions
        {
            FacadeScope = " ",
            ClientId = "client",
            ClientAssertionJwk = key,
            DPoPJwk = key
        });

        Assert.True(result.Failed);
        Assert.Contains("HelseId:FacadeScope is required.", result.Failures);
    }

    [Fact]
    public async Task Development_test_mode_requests_a_dpop_client_credentials_token_for_dhg()
    {
        var key = PrivateJwk();
        var handler = new TokenEndpointHandler();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.Configure<HelseIdOptions>(options =>
        {
            options.Authority = new Uri("https://helseid-sts.test.nhn.no");
            options.ClientId = "dhg-test-client";
            options.ClientAssertionJwk = key;
            options.DPoPJwk = key;
            options.DhgAudience = "nhn:maternity-record";
            options.DhgScope = "nhn:maternity-record/api";
        });
        services.Configure<DevelopmentTestModeOptions>(options => options.Enabled = true);
        services.AddClientCredentialsTokenManagement();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IHelseIdClientAssertionFactory, HelseIdClientAssertionFactory>();
        services.AddHttpClient("HelseIdBackchannel")
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        services.AddScoped<HelseIdAuthorizationProvider>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var authorizationProvider = scope.ServiceProvider.GetRequiredService<HelseIdAuthorizationProvider>();

        var authorization = await authorizationProvider.AuthorizeAsync(
            string.Empty,
            HttpMethod.Get,
            new Uri("https://maternity-record.hit.test.nhn.no/api/maternity-record/v1/status"),
            null,
            CancellationToken.None);

        Assert.Equal("server-access-token", authorization.AccessToken);
        Assert.Contains("grant_type=client_credentials", handler.RequestBody);
        Assert.Contains("scope=nhn%3Amaternity-record%2Fapi", handler.RequestBody);
        Assert.Contains("resource=nhn%3Amaternity-record", handler.RequestBody);
        Assert.DoesNotContain("subject_token", handler.RequestBody);
        Assert.False(string.IsNullOrWhiteSpace(handler.DPoPProof));
    }

    private static DhgHttpClient Client(
        HttpMessageHandler handler,
        IDhgAuthorizationProvider authorization,
        int maxTransientRetries = 0) => new(
        new HttpClient(handler) { BaseAddress = new Uri("https://maternity-record.hit.test.nhn.no/api/maternity-record/v1/") },
        authorization,
        Options.Create(new DhgOptions
        {
            SourceSystem = "source/1.0",
            MaxTransientRetries = maxTransientRetries
        }));

    private static PatientRequestContext Context() =>
        new("patient-1", "01019012345", "subject-token", "17a80c64-4592-48ad-ae7a-f537e8863dc1");

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string json) => new(statusCode)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    };

    private static string PrivateJwk()
    {
        using var rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa.ExportParameters(true)) { KeyId = Guid.NewGuid().ToString("N") };
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(key);
        jwk.Alg = SecurityAlgorithms.RsaSsaPssSha256;
        return JsonSerializer.Serialize(jwk);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class RecordingAuthorizationProvider : IDhgAuthorizationProvider
    {
        public List<string?> Nonces { get; } = [];

        public Task<DhgAuthorization> AuthorizeAsync(
            string subjectToken,
            HttpMethod method,
            Uri destination,
            string? dPoPNonce,
            CancellationToken cancellationToken)
        {
            Nonces.Add(dPoPNonce);
            return Task.FromResult(new DhgAuthorization("access-token", "proof"));
        }
    }

    private sealed record RecordedRequest(
        Uri Uri,
        string? PatientNin,
        string? SourceSystem,
        string? AuthorizationScheme);

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new RecordedRequest(
                request.RequestUri!,
                request.Headers.TryGetValues("nhn-patient-nin", out var nin) ? nin.Single() : null,
                request.Headers.TryGetValues("nhn-source-system", out var source) ? source.Single() : null,
                request.Headers.Authorization?.Scheme));
            return Task.FromResult(response(request));
        }
    }

    private sealed class TokenEndpointHandler : HttpMessageHandler
    {
        public string RequestBody { get; private set; } = string.Empty;
        public string? DPoPProof { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            DPoPProof = request.Headers.TryGetValues("DPoP", out var proof) ? proof.SingleOrDefault() : null;
            return Json(HttpStatusCode.OK,
                "{\"access_token\":\"server-access-token\",\"token_type\":\"DPoP\",\"expires_in\":300}");
        }
    }
}
