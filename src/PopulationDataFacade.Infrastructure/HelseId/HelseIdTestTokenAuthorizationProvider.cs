using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PopulationDataFacade.Core;
using PopulationDataFacade.Infrastructure.Configuration;
using PopulationDataFacade.Infrastructure.Dhg;

namespace PopulationDataFacade.Infrastructure.HelseId;

/// <summary>
/// TEST-only adapter for HelseIDs test-token utility. Den ber utility om et nytt
/// access token og proof bundet til hver eksakte DHG request, i samsvar med etablert
/// smartOppgave test flow. Ingen av verdiene persisteres eller logges.
/// </summary>
public sealed class HelseIdTestTokenAuthorizationProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<HelseIdTestTokenOptions> options,
    IOptions<HelseIdOptions> helseIdOptions,
    IOptions<DhgOptions> dhgOptions) : IDhgAuthorizationProvider
{
    private const int MaximumResponseBytes = 64 << 10;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
    private static readonly ActivitySource ActivitySource = new("PopulationDataFacade.HelseIdTestToken");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<DhgAuthorization> AuthorizeAsync(
        string subjectToken,
        HttpMethod method,
        Uri destination,
        string? dPoPNonce,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(dPoPNonce))
            throw new PopulationDataException(
                PopulationErrorKind.SourceUnavailable,
                "The HelseID TEST token utility cannot satisfy a DHG DPoP nonce challenge.");
        if (!destination.IsAbsoluteUri || destination.Scheme != Uri.UriSchemeHttps)
            throw new PopulationDataException(
                PopulationErrorKind.ConfigurationInvalid,
                "The HelseID TEST token utility requires an absolute HTTPS DHG target.");

        var configuration = options.Value;
        var normalizedMethod = method.Method.ToUpperInvariant();
        var statusRequest = IsStatusRequest(destination, dhgOptions.Value.BaseUrl);
        using var activity = ActivitySource.StartActivity("HelseID TEST token", ActivityKind.Client);
        activity?.SetTag("server.address", configuration.Endpoint.Host);
        activity?.SetTag("http.request.method", "POST");
        activity?.SetTag("dpop.target.host", destination.Host);
        activity?.SetTag("dpop.target.method", normalizedMethod);

        var clientClaims = new Dictionary<string, object?>
        {
            ["scope"] = new[] { configuration.Scope },
            ["clientId"] = helseIdOptions.Value.ClientId,
            ["orgnrParent"] = configuration.OrgnrParent,
            ["orgnrChild"] = configuration.OrgnrChild,
            ["clientTenancy"] = configuration.ClientTenancy,
            ["clientAuthenticationMethodsReferences"] = "private_key_jwt",
            ["clientName"] = configuration.ClientName
        };
        if (configuration.ClientTenancyType is { } tenancyType)
            clientClaims["clientTenancyType"] = tenancyType;

        var payload = new Dictionary<string, object?>
        {
            ["audience"] = configuration.Audience,
            ["issuerEnvironment"] = 0,
            ["withoutDefaultClientClaims"] = true,
            ["withoutDefaultUserClaims"] = true,
            ["expirationParameters"] = new { expirationTimeInSeconds = 600 },
            ["headerParameters"] = new { typ = "at+jwt" },
            ["createDPoPTokenWithDPoPProof"] = true,
            ["dPoPProofParameters"] = new
            {
                htmClaimValue = normalizedMethod,
                htuClaimValue = destination.ToString()
            },
            ["clientClaimsParameters"] = clientClaims
        };
        if (!statusRequest)
        {
            payload["userClaimsParameters"] = new
            {
                pid = configuration.PractitionerNationalIdentityNumber,
                hprNumber = configuration.PractitionerHprNumber,
                name = configuration.PractitionerName,
                securityLevel = "4",
                network = "internett"
            };
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, configuration.Endpoint)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        try
        {
            request.Headers.Add("x-auth-key", configuration.AuthKey);
        }
        catch (FormatException exception)
        {
            throw new PopulationDataException(
                PopulationErrorKind.ConfigurationInvalid,
                "HelseIdTestToken:AuthKey contains an invalid header value.",
                exception);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);
        HttpResponseMessage response;
        try
        {
            response = await httpClientFactory.CreateClient("HelseIdTestToken")
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new PopulationDataException(
                PopulationErrorKind.SourceUnavailable,
                "The HelseID TEST token request timed out.",
                exception);
        }
        catch (HttpRequestException exception)
        {
            throw new PopulationDataException(
                PopulationErrorKind.SourceUnavailable,
                "The HelseID TEST token utility is unavailable.",
                exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw MapFailure(response.StatusCode);

            try
            {
                var responseBytes = await ReadBoundedAsync(response.Content, timeout.Token);
                using var document = JsonDocument.Parse(responseBytes);
                var token = FindString(document.RootElement, "access_token", "accessToken", "token");
                var proof = FindString(document.RootElement, "dPoPProof", "DPoPProof", "dpopProof");
                if (TryGetProperty(document.RootElement, "successResponse", out var success))
                {
                    token ??= FindString(success, "accessTokenJwt", "AccessTokenJwt");
                    proof ??= FindString(success, "dPoPProof", "DPoPProof", "dpopProof");
                }

                if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(proof))
                    throw new PopulationDataException(
                        PopulationErrorKind.SourceUnavailable,
                        "The HelseID TEST token response did not contain both an access token and DPoP proof.");

                if (statusRequest)
                    return new DhgAuthorization(token, proof);

                var role = JsonSerializer.Serialize(new
                {
                    system = configuration.UserRoleSystem,
                    code = configuration.UserRoleCode
                }, JsonOptions);
                return new DhgAuthorization(
                    token,
                    proof,
                    Uri.EscapeDataString(role),
                    Uri.EscapeDataString(configuration.TreatmentFacilityName));
            }
            catch (PopulationDataException)
            {
                throw;
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new PopulationDataException(
                    PopulationErrorKind.SourceUnavailable,
                    "The HelseID TEST token response timed out.",
                    exception);
            }
            catch (Exception exception) when (exception is JsonException or IOException or
                                              HttpRequestException or InvalidOperationException)
            {
                throw new PopulationDataException(
                    PopulationErrorKind.SourceUnavailable,
                    "The HelseID TEST token response was invalid.",
                    exception);
            }
        }
    }

    private static bool IsStatusRequest(Uri destination, Uri baseUrl)
    {
        var statusEndpoint = new Uri(baseUrl, "status");
        return Uri.Compare(
            destination,
            statusEndpoint,
            UriComponents.SchemeAndServer | UriComponents.Path,
            UriFormat.SafeUnescaped,
            StringComparison.OrdinalIgnoreCase) == 0;
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > MaximumResponseBytes)
            throw new InvalidOperationException("The HelseID TEST token response exceeded the size limit.");

        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[MaximumResponseBytes + 1];
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken);
            if (read == 0) break;
            total += read;
        }
        if (total > MaximumResponseBytes)
            throw new InvalidOperationException("The HelseID TEST token response exceeded the size limit.");
        return buffer[..total];
    }

    private static string? FindString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(value.GetString()))
                return value.GetString()!.Trim();
        }
        return null;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }
        value = default;
        return false;
    }

    private static PopulationDataException MapFailure(HttpStatusCode statusCode)
    {
        if (statusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return new PopulationDataException(
                PopulationErrorKind.ConfigurationInvalid,
                $"HelseID TEST token request was rejected ({(int)statusCode}).");
        if (statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
            (int)statusCode == 425 ||
            statusCode >= HttpStatusCode.InternalServerError)
            return new PopulationDataException(
                PopulationErrorKind.SourceUnavailable,
                $"HelseID TEST token utility is temporarily unavailable ({(int)statusCode}).");
        return new PopulationDataException(
            PopulationErrorKind.SourceUnavailable,
            $"HelseID TEST token request failed ({(int)statusCode}).");
    }
}
