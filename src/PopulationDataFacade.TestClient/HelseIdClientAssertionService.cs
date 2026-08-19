using System.Security.Claims;
using Duende.AccessTokenManagement;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace PopulationDataFacade.TestClient;

public sealed class TestClientOptions
{
    public const string SectionName = "TestClient";
    public Uri FacadeBaseUrl { get; set; } = new("https://localhost:7184/");
    public Uri Authority { get; set; } = new("https://helseid-sts.test.nhn.no");
    public string ClientId { get; set; } = string.Empty;
    public string Audience { get; set; } = "nhn:population-data-facade";
    public string Scope { get; set; } = "nhn:population-data-facade/read";
    public string ClientAssertionJwk { get; set; } = string.Empty;
    public string DPoPJwk { get; set; } = string.Empty;
    public string DefaultPatientAlias { get; set; } = "synthetic-1";
    public string PatientContextHeaderName { get; set; } = "X-Patient-Context";
}

public sealed class HelseIdClientAssertionService(
    IOptions<TestClientOptions> options,
    TimeProvider timeProvider) : IClientAssertionService
{
    public Task<ClientAssertion?> GetClientAssertionAsync(
        ClientCredentialsClientName? clientName = null,
        TokenRequestParameters? parameters = null,
        CancellationToken ct = default)
    {
        var configuration = options.Value;
        var key = new JsonWebKey(configuration.ClientAssertionJwk);
        if (!key.HasPrivateKey) throw new InvalidOperationException("TestClient:ClientAssertionJwk must contain a private key.");
        var now = timeProvider.GetUtcNow();
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = configuration.ClientId,
            Audience = configuration.Authority.GetLeftPart(UriPartial.Authority).TrimEnd('/'),
            Subject = new ClaimsIdentity(
                [new("sub", configuration.ClientId), new("jti", Guid.NewGuid().ToString("N"))]),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.AddSeconds(8).UtcDateTime,
            SigningCredentials = new SigningCredentials(
                key,
                string.IsNullOrWhiteSpace(key.Alg) ? SecurityAlgorithms.RsaSsaPssSha256 : key.Alg),
            TokenType = "client-authentication+jwt"
        };

        return Task.FromResult<ClientAssertion?>(new ClientAssertion
        {
            Type = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            Value = new JsonWebTokenHandler().CreateToken(descriptor)
        });
    }
}
