using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using PopulationDataFacade.Core;
using Task = System.Threading.Tasks.Task;

namespace PopulationDataFacade.Api.Fhir;

public static class FhirHttp
{
    private static readonly FhirJsonSerializer Serializer = new();

    public static IResult Result(Resource resource, int statusCode = StatusCodes.Status200OK) =>
        new FhirResourceResult(resource, statusCode);

    public static OperationOutcome Outcome(string code, string diagnostics, OperationOutcome.IssueSeverity severity = OperationOutcome.IssueSeverity.Error) =>
        new()
        {
            Issue =
            [
                new OperationOutcome.IssueComponent
                {
                    Severity = severity,
                    Code = Enum.TryParse<OperationOutcome.IssueType>(code.Replace("-", string.Empty), true, out var issueType)
                        ? issueType
                        : OperationOutcome.IssueType.Processing,
                    Diagnostics = diagnostics
                }
            ]
        };

    private sealed class FhirResourceResult(Resource resource, int statusCode) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = statusCode;
            httpContext.Response.ContentType = "application/fhir+json; charset=utf-8";
            httpContext.Response.Headers.CacheControl = "no-store";
            await httpContext.Response.WriteAsync(Serializer.SerializeToString(resource), httpContext.RequestAborted);
        }
    }
}

public sealed class FhirExceptionMiddleware(RequestDelegate next, ILogger<FhirExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (PopulationDataException exception)
        {
            var (status, issue) = Map(exception.Kind);
            logger.LogWarning("Population request failed with {ErrorKind}; correlation {CorrelationId}.", exception.Kind, context.TraceIdentifier);
            await FhirHttp.Result(FhirHttp.Outcome(issue, exception.Message), status).ExecuteAsync(context);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unhandled population facade failure; correlation {CorrelationId}.", context.TraceIdentifier);
            await FhirHttp.Result(
                FhirHttp.Outcome("exception", $"An unexpected error occurred. Correlation ID: {context.TraceIdentifier}"),
                StatusCodes.Status500InternalServerError).ExecuteAsync(context);
        }
    }

    private static (int Status, string Issue) Map(PopulationErrorKind kind) => kind switch
    {
        PopulationErrorKind.InvalidPatientContext => (StatusCodes.Status400BadRequest, "invalid"),
        PopulationErrorKind.Unauthorized => (StatusCodes.Status401Unauthorized, "security"),
        PopulationErrorKind.Forbidden or PopulationErrorKind.ConsentMissing => (StatusCodes.Status403Forbidden, "forbidden"),
        PopulationErrorKind.NotFound or PopulationErrorKind.NoActiveMaternityRecord => (StatusCodes.Status404NotFound, "not-found"),
        PopulationErrorKind.RateLimited => (StatusCodes.Status429TooManyRequests, "throttled"),
        PopulationErrorKind.ConfigurationInvalid => (StatusCodes.Status500InternalServerError, "exception"),
        _ => (StatusCodes.Status503ServiceUnavailable, "transient")
    };
}
