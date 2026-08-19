using System.Diagnostics;
using Duende.AccessTokenManagement;
using Duende.AccessTokenManagement.DPoP;
using Duende.IdentityModel.Client;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PopulationDataFacade.Core;
using PopulationDataFacade.Infrastructure.Configuration;
using PopulationDataFacade.Infrastructure.Dhg;

namespace PopulationDataFacade.Infrastructure.HelseId;

public interface IHelseIdClientAssertionFactory
{
    ClientAssertion Create(Uri audience);
}

public sealed class HelseIdClientAssertionFactory(
    IOptions<HelseIdOptions> options,
    TimeProvider timeProvider) : IHelseIdClientAssertionFactory
{
    public ClientAssertion Create(Uri audience)
    {
        var configuration = options.Value;
        var key = new JsonWebKey(configuration.ClientAssertionJwk);
        if (!key.HasPrivateKey)
            throw new PopulationDataException(PopulationErrorKind.ConfigurationInvalid, "The HelseID client assertion JWK has no private key.");

        var now = timeProvider.GetUtcNow();
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = configuration.ClientId,
            Audience = audience.GetLeftPart(UriPartial.Authority).TrimEnd('/'),
            Subject = new System.Security.Claims.ClaimsIdentity(
                [new("sub", configuration.ClientId), new("jti", Guid.NewGuid().ToString("N"))]),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.AddSeconds(8).UtcDateTime,
            SigningCredentials = new SigningCredentials(
                key,
                string.IsNullOrWhiteSpace(key.Alg) ? SecurityAlgorithms.RsaSsaPssSha256 : key.Alg),
            TokenType = "client-authentication+jwt"
        };

        return new ClientAssertion
        {
            Type = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            Value = new JsonWebTokenHandler().CreateToken(descriptor)
        };
    }
}

public sealed class HelseIdAuthorizationProvider(
    IHttpClientFactory httpClientFactory,
    IDPoPProofService proofService,
    IHelseIdClientAssertionFactory assertionFactory,
    IOptions<HelseIdOptions> options,
    IOptions<DevelopmentTestModeOptions> developmentTestMode) : IDhgAuthorizationProvider
{
    private static readonly ActivitySource ActivitySource = new("PopulationDataFacade.HelseId");

    public async Task<DhgAuthorization> AuthorizeAsync(
        string subjectToken,
        HttpMethod method,
        Uri destination,
        string? dPoPNonce,
        CancellationToken cancellationToken)
    {
        if (!developmentTestMode.Value.Enabled && string.IsNullOrWhiteSpace(subjectToken))
            throw new PopulationDataException(PopulationErrorKind.Unauthorized, "An incoming HelseID access token is required.");

        var configuration = options.Value;
        var tokenEndpoint = new Uri(configuration.Authority, "/connect/token");
        var proofKey = ParseProofKey(configuration.DPoPJwk);

        var useClientCredentials = developmentTestMode.Value.Enabled;
        using var activity = ActivitySource.StartActivity(
            useClientCredentials ? "HelseID client credentials" : "HelseID token exchange",
            ActivityKind.Client);
        activity?.SetTag("server.address", tokenEndpoint.Host);
        activity?.SetTag("oauth.grant_type", useClientCredentials ? "client_credentials" : "token_exchange");

        var response = useClientCredentials
            ? await RequestClientCredentialsAsync(tokenEndpoint, proofKey, null, cancellationToken)
            : await ExchangeAsync(subjectToken, tokenEndpoint, proofKey, null, cancellationToken);
        if (response.IsError && response.DPoPNonce is not null &&
            response.Error is "use_dpop_nonce" or "invalid_dpop_proof")
        {
            var nonce = DPoPNonce.Parse(response.DPoPNonce);
            response = useClientCredentials
                ? await RequestClientCredentialsAsync(tokenEndpoint, proofKey, nonce, cancellationToken)
                : await ExchangeAsync(subjectToken, tokenEndpoint, proofKey, nonce, cancellationToken);
        }

        if (response.IsError || string.IsNullOrWhiteSpace(response.AccessToken))
            throw MapTokenError(response.Error, useClientCredentials);

        var apiProof = await proofService.CreateProofTokenAsync(new DPoPProofRequest
        {
            Url = destination,
            Method = method,
            DPoPProofKey = proofKey,
            DPoPNonce = string.IsNullOrWhiteSpace(dPoPNonce) ? null : DPoPNonce.Parse(dPoPNonce),
            AccessToken = AccessToken.Parse(response.AccessToken)
        }, cancellationToken);

        if (apiProof is null)
            throw new PopulationDataException(PopulationErrorKind.ConfigurationInvalid, "A DPoP proof could not be created.");

        return new DhgAuthorization(response.AccessToken, apiProof.Value);
    }

    private async Task<TokenResponse> RequestClientCredentialsAsync(
        Uri tokenEndpoint,
        DPoPProofKey proofKey,
        DPoPNonce? nonce,
        CancellationToken cancellationToken)
    {
        var proof = await proofService.CreateProofTokenAsync(new DPoPProofRequest
        {
            Url = tokenEndpoint,
            Method = HttpMethod.Post,
            DPoPProofKey = proofKey,
            DPoPNonce = nonce
        }, cancellationToken);

        var request = new ClientCredentialsTokenRequest
        {
            Address = tokenEndpoint.ToString(),
            ClientId = options.Value.ClientId,
            ClientAssertion = assertionFactory.Create(options.Value.Authority),
            ClientCredentialStyle = ClientCredentialStyle.PostBody,
            Resource = [options.Value.DhgAudience],
            Scope = options.Value.DhgScope,
            DPoPProofToken = proof?.ToString()
        };

        return await httpClientFactory.CreateClient("HelseIdBackchannel")
            .RequestClientCredentialsTokenAsync(request, cancellationToken);
    }

    private async Task<TokenResponse> ExchangeAsync(
        string subjectToken,
        Uri tokenEndpoint,
        DPoPProofKey proofKey,
        DPoPNonce? nonce,
        CancellationToken cancellationToken)
    {
        var proof = await proofService.CreateProofTokenAsync(new DPoPProofRequest
        {
            Url = tokenEndpoint,
            Method = HttpMethod.Post,
            DPoPProofKey = proofKey,
            DPoPNonce = nonce
        }, cancellationToken);

        var request = new TokenExchangeTokenRequest
        {
            Address = tokenEndpoint.ToString(),
            ClientId = options.Value.ClientId,
            ClientAssertion = assertionFactory.Create(options.Value.Authority),
            ClientCredentialStyle = ClientCredentialStyle.PostBody,
            SubjectToken = subjectToken,
            SubjectTokenType = "urn:ietf:params:oauth:token-type:access_token",
            Audience = options.Value.DhgAudience,
            Scope = options.Value.DhgScope,
            DPoPProofToken = proof?.ToString()
        };

        return await httpClientFactory.CreateClient("HelseIdBackchannel")
            .RequestTokenExchangeTokenAsync(request, cancellationToken);
    }

    private static DPoPProofKey ParseProofKey(string value)
    {
        try
        {
            return DPoPProofKey.Parse(value);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            throw new PopulationDataException(PopulationErrorKind.ConfigurationInvalid, "The configured HelseID DPoP JWK is invalid.", exception);
        }
    }

    private static PopulationDataException MapTokenError(string? error, bool clientCredentials) => error switch
    {
        "invalid_client" => new(PopulationErrorKind.ConfigurationInvalid, "HelseID rejected the facade client authentication."),
        "invalid_grant" or "invalid_request" when clientCredentials => new(PopulationErrorKind.ConfigurationInvalid, "HelseID rejected the DHG client-credentials request."),
        "invalid_grant" or "invalid_request" => new(PopulationErrorKind.Unauthorized, "HelseID rejected the delegated subject token."),
        "invalid_scope" or "invalid_target" => new(PopulationErrorKind.ConfigurationInvalid, "HelseID rejected the configured DHG target or scope."),
        _ => new(PopulationErrorKind.SourceUnavailable, "HelseID token request failed.")
    };
}
