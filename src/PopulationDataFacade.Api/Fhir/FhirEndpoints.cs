using Hl7.Fhir.Model;
using PopulationDataFacade.Api.Security;
using PopulationDataFacade.Core;

namespace PopulationDataFacade.Api.Fhir;

public static class FhirEndpoints
{
    public static RouteGroupBuilder MapPopulationFhirApi(
        this IEndpointRouteBuilder endpoints,
        bool requireAuthorization = true)
    {
        var group = endpoints.MapGroup("/fhir")
            .WithTags("FHIR R4");

        if (requireAuthorization) group.RequireAuthorization("population.read");

        group.MapGet("/metadata", (HttpContext context, IFhirPopulationMapper mapper) =>
                FhirHttp.Result(mapper.CapabilityStatement(ServiceBase(context))))
            .AllowAnonymous()
            .WithName("CapabilityStatement")
            .Produces(StatusCodes.Status200OK, contentType: "application/fhir+json");

        group.MapGet("/Patient/{id}", GetPatientAsync)
            .WithName("ReadPatient")
            .Produces(StatusCodes.Status200OK, contentType: "application/fhir+json")
            .Produces(StatusCodes.Status404NotFound, contentType: "application/fhir+json");

        group.MapGet("/Observation", SearchObservationsAsync)
            .WithName("SearchObservations")
            .Produces(StatusCodes.Status200OK, contentType: "application/fhir+json");

        group.MapGet("/Encounter", SearchEncountersAsync)
            .WithName("SearchEncounters")
            .Produces(StatusCodes.Status200OK, contentType: "application/fhir+json");

        return group;
    }

    private static async Task<IResult> GetPatientAsync(
        string id,
        HttpContext httpContext,
        PatientRequestContextFactory contextFactory,
        IPopulationDataService service,
        IFhirPopulationMapper mapper,
        CancellationToken cancellationToken)
    {
        var requestContext = contextFactory.Create(httpContext, id);
        var snapshot = await service.GetSnapshotAsync(requestContext, cancellationToken);
        return FhirHttp.Result(mapper.MapPatient(snapshot.Patient));
    }

    private static async Task<IResult> SearchObservationsAsync(
        string? patient,
        string? code,
        HttpContext httpContext,
        PatientRequestContextFactory contextFactory,
        IPopulationDataService service,
        IFhirPopulationMapper mapper,
        CancellationToken cancellationToken)
    {
        var patientId = RequiredPatient(patient);
        var requestContext = contextFactory.Create(httpContext, patientId);
        var snapshot = await service.GetSnapshotAsync(requestContext, cancellationToken);
        var filter = ParseCode(code);
        var resources = mapper.MapObservations(snapshot, filter).Cast<Resource>();
        return FhirHttp.Result(mapper.SearchBundle(resources, ServiceBase(httpContext)));
    }

    private static async Task<IResult> SearchEncountersAsync(
        string? patient,
        HttpContext httpContext,
        PatientRequestContextFactory contextFactory,
        IPopulationDataService service,
        IFhirPopulationMapper mapper,
        CancellationToken cancellationToken)
    {
        var patientId = RequiredPatient(patient);
        var requestContext = contextFactory.Create(httpContext, patientId);
        var snapshot = await service.GetSnapshotAsync(requestContext, cancellationToken);
        return FhirHttp.Result(mapper.SearchBundle(mapper.MapEncounters(snapshot).Cast<Resource>(), ServiceBase(httpContext)));
    }

    private static string RequiredPatient(string? patient)
    {
        if (string.IsNullOrWhiteSpace(patient))
            throw new PopulationDataException(PopulationErrorKind.InvalidPatientContext, "The patient search parameter is required.");
        return patient.StartsWith("Patient/", StringComparison.Ordinal) ? patient[8..] : patient;
    }

    private static PopulationCode? ParseCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var separator = code.IndexOf('|');
        if (separator <= 0 || separator == code.Length - 1)
            throw new PopulationDataException(PopulationErrorKind.InvalidPatientContext, "Code must use the system|code token form.");
        return new PopulationCode(code[..separator], code[(separator + 1)..], string.Empty);
    }

    private static Uri ServiceBase(HttpContext context) =>
        new($"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}/");
}
