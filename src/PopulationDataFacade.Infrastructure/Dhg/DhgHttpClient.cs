using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PopulationDataFacade.Core;
using PopulationDataFacade.Infrastructure.Configuration;

namespace PopulationDataFacade.Infrastructure.Dhg;

public sealed class DhgHttpClient(
    HttpClient httpClient,
    IDhgAuthorizationProvider authorizationProvider,
    IOptions<DhgOptions> options) : IDhgClient
{
    private static readonly ActivitySource ActivitySource = new("PopulationDataFacade.Dhg");
    private static readonly Meter Meter = new("PopulationDataFacade.Dhg");
    private static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("dhg.request.duration", "ms");
    private static readonly Counter<long> Errors = Meter.CreateCounter<long>("dhg.request.errors");

    public Task<DhgStatusResponse> GetStatusAsync(PatientRequestContext context, CancellationToken cancellationToken) =>
        GetAsync<DhgStatusResponse>("status", context, cancellationToken);

    public Task<DhgMaternityRecord> GetRecordAsync(string recordId, PatientRequestContext context, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(recordId, out _))
            throw new PopulationDataException(PopulationErrorKind.SourceContractInvalid, "DHG returned an invalid latestRecordId.");
        return GetAsync<DhgMaternityRecord>($"record/{Uri.EscapeDataString(recordId)}", context, cancellationToken);
    }

    private async Task<T> GetAsync<T>(string relativePath, PatientRequestContext context, CancellationToken cancellationToken)
    {
        var destination = new Uri(httpClient.BaseAddress!, relativePath);
        var retryCount = options.Value.MaxTransientRetries;
        string? dPoPNonce = null;
        var resourceNonceRetried = false;
        for (var attempt = 0; ; attempt++)
        {
            using var activity = ActivitySource.StartActivity("DHG GET", ActivityKind.Client);
            activity?.SetTag("http.request.method", "GET");
            activity?.SetTag("server.address", destination.Host);
            activity?.SetTag("dhg.operation", TelemetryOperation(relativePath));
            activity?.SetTag("dhg.retry.attempt", attempt);
            var started = Stopwatch.GetTimestamp();

            using var request = await CreateRequestAsync(destination, context, dPoPNonce, cancellationToken);
            HttpResponseMessage response;
            try
            {
                response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt < retryCount)
                {
                    await DelayAsync(attempt, null, cancellationToken);
                    continue;
                }
                Errors.Add(1, new KeyValuePair<string, object?>("reason", "timeout"));
                throw new PopulationDataException(PopulationErrorKind.SourceUnavailable, "DHG request timed out.");
            }
            catch (HttpRequestException exception)
            {
                if (attempt < retryCount)
                {
                    await DelayAsync(attempt, null, cancellationToken);
                    continue;
                }
                Errors.Add(1, new KeyValuePair<string, object?>("reason", "network"));
                throw new PopulationDataException(PopulationErrorKind.SourceUnavailable, "DHG is unavailable.", exception);
            }

            using (response)
            {
                Duration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
                activity?.SetTag("http.response.status_code", (int)response.StatusCode);
                if (response.StatusCode == HttpStatusCode.Unauthorized && !resourceNonceRetried &&
                    response.Headers.TryGetValues("DPoP-Nonce", out var nonceValues))
                {
                    dPoPNonce = nonceValues.SingleOrDefault();
                    if (!string.IsNullOrWhiteSpace(dPoPNonce))
                    {
                        resourceNonceRetried = true;
                        continue;
                    }
                }
                if (IsTransient(response.StatusCode) && attempt < retryCount)
                {
                    await DelayAsync(attempt, response.Headers.RetryAfter, cancellationToken);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    Errors.Add(1, new KeyValuePair<string, object?>("reason", $"http-{(int)response.StatusCode}"));
                    throw MapError(response.StatusCode);
                }

                try
                {
                    await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    var result = await JsonSerializer.DeserializeAsync<T>(stream, DhgJson.Options, cancellationToken);
                    return result ?? throw new JsonException("DHG returned an empty JSON document.");
                }
                catch (JsonException exception)
                {
                    Errors.Add(1, new KeyValuePair<string, object?>("reason", "contract"));
                    throw new PopulationDataException(PopulationErrorKind.SourceContractInvalid, "DHG response did not match the documented contract.", exception);
                }
            }
        }
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(Uri destination, PatientRequestContext context, string? dPoPNonce, CancellationToken cancellationToken)
    {
        var authorization = await authorizationProvider.AuthorizeAsync(context.SubjectToken, HttpMethod.Get, destination, dPoPNonce, cancellationToken);
        var request = new HttpRequestMessage(HttpMethod.Get, destination);
        request.Headers.Authorization = new AuthenticationHeaderValue("DPoP", authorization.AccessToken);
        request.Headers.TryAddWithoutValidation("DPoP", authorization.DPoPProof);
        request.Headers.TryAddWithoutValidation("nhn-source-system", options.Value.SourceSystem);
        request.Headers.TryAddWithoutValidation("nhn-patient-nin", context.NationalIdentityNumber);
        request.Headers.TryAddWithoutValidation("nhn-event-id", context.CorrelationId);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static bool IsTransient(HttpStatusCode statusCode) => statusCode is
        HttpStatusCode.RequestTimeout or
        HttpStatusCode.TooManyRequests or
        HttpStatusCode.BadGateway or
        HttpStatusCode.ServiceUnavailable or
        HttpStatusCode.GatewayTimeout;

    private static PopulationDataException MapError(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.Unauthorized => new(PopulationErrorKind.Unauthorized, "DHG authorization failed."),
        HttpStatusCode.Forbidden => new(PopulationErrorKind.Forbidden, "DHG denied access."),
        HttpStatusCode.NotFound => new(PopulationErrorKind.NotFound, "The active DHG record was not found."),
        HttpStatusCode.TooManyRequests => new(PopulationErrorKind.RateLimited, "DHG rate limit was reached."),
        HttpStatusCode.BadRequest => new(PopulationErrorKind.SourceContractInvalid, "DHG rejected the request."),
        _ when IsTransient(statusCode) => new(PopulationErrorKind.SourceUnavailable, "DHG is temporarily unavailable."),
        _ => new(PopulationErrorKind.SourceUnavailable, "DHG returned an unexpected error.")
    };

    private static Task DelayAsync(int attempt, RetryConditionHeaderValue? retryAfter, CancellationToken cancellationToken)
    {
        var delay = CalculateRetryDelay(attempt, retryAfter, DateTimeOffset.UtcNow);
        return Task.Delay(delay, cancellationToken);
    }

    internal static TimeSpan CalculateRetryDelay(int attempt, RetryConditionHeaderValue? retryAfter, DateTimeOffset now)
    {
        var requested = retryAfter?.Delta;
        if (requested is null && retryAfter?.Date is { } date)
            requested = date - now;

        if (requested is not null)
            return requested.Value <= TimeSpan.Zero ? TimeSpan.Zero : requested.Value;

        return TimeSpan.FromMilliseconds(
            Math.Min(2000, 150 * Math.Pow(2, attempt)) + Random.Shared.Next(25, 175));
    }

    private static string TelemetryOperation(string relativePath) =>
        relativePath.StartsWith("record/", StringComparison.Ordinal) ? "record" : "status";
}
