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

        group.MapPost("/Patient/_search", SearchPatientByIdentifierAsync)
            .WithName("SearchPatientByIdentifier")
            .WithDescription("FHIR POST search med NIN i form body. Krever HelseID utenfor lokal DevelopmentTestMode. NIN returneres aldri.")
            .Produces(StatusCodes.Status200OK, contentType: "application/fhir+json")
            .Produces(StatusCodes.Status400BadRequest, contentType: "application/fhir+json")
            .Produces(StatusCodes.Status404NotFound, contentType: "application/fhir+json");

        group.MapPost("/Observation/_search", SearchObservationsByPatientIdentifierAsync)
            .WithName("SearchObservationsByPatientIdentifier")
            .WithDescription("FHIR POST search med patient NIN i form body. Krever HelseID utenfor lokal DevelopmentTestMode. NIN returneres aldri.")
            .Produces(StatusCodes.Status200OK, contentType: "application/fhir+json")
            .Produces(StatusCodes.Status400BadRequest, contentType: "application/fhir+json")
            .Produces(StatusCodes.Status404NotFound, contentType: "application/fhir+json");

        group.MapPost("/Encounter/_search", SearchEncountersByPatientIdentifierAsync)
            .WithName("SearchEncountersByPatientIdentifier")
            .WithDescription("FHIR POST search med patient NIN i form body. Krever HelseID utenfor lokal DevelopmentTestMode. NIN returneres aldri.")
            .Produces(StatusCodes.Status200OK, contentType: "application/fhir+json")
            .Produces(StatusCodes.Status400BadRequest, contentType: "application/fhir+json")
            .Produces(StatusCodes.Status404NotFound, contentType: "application/fhir+json");

        return group;
    }

    private static async Task<IResult> SearchPatientByIdentifierAsync(
        HttpContext httpContext,
        PatientRequestContextFactory contextFactory,
        IPopulationDataService service,
        IFhirPopulationMapper mapper,
        CancellationToken cancellationToken)
    {
        var form = await ReadSearchFormAsync(httpContext, cancellationToken, "identifier");
        var requestContext = contextFactory.CreateForNinSearch(
            httpContext,
            RequiredSingleValue(form, "identifier"));
        var snapshot = await service.GetSnapshotAsync(requestContext, cancellationToken);
        return FhirHttp.Result(mapper.SearchBundle([mapper.MapPatient(snapshot.Patient)], ServiceBase(httpContext)));
    }

    private static async Task<IResult> SearchObservationsByPatientIdentifierAsync(
        HttpContext httpContext,
        PatientRequestContextFactory contextFactory,
        IPopulationDataService service,
        IFhirPopulationMapper mapper,
        CancellationToken cancellationToken)
    {
        var form = await ReadSearchFormAsync(httpContext, cancellationToken, "patient.identifier", "code");
        var requestContext = contextFactory.CreateForNinSearch(
            httpContext,
            RequiredSingleValue(form, "patient.identifier"));
        var snapshot = await service.GetSnapshotAsync(requestContext, cancellationToken);
        var resources = mapper.MapObservations(snapshot, ParseCode(OptionalSingleValue(form, "code"))).Cast<Resource>();
        return FhirHttp.Result(mapper.SearchBundle(resources, ServiceBase(httpContext)));
    }

    private static async Task<IResult> SearchEncountersByPatientIdentifierAsync(
        HttpContext httpContext,
        PatientRequestContextFactory contextFactory,
        IPopulationDataService service,
        IFhirPopulationMapper mapper,
        CancellationToken cancellationToken)
    {
        var form = await ReadSearchFormAsync(httpContext, cancellationToken, "patient.identifier");
        var requestContext = contextFactory.CreateForNinSearch(
            httpContext,
            RequiredSingleValue(form, "patient.identifier"));
        var snapshot = await service.GetSnapshotAsync(requestContext, cancellationToken);
        return FhirHttp.Result(mapper.SearchBundle(mapper.MapEncounters(snapshot).Cast<Resource>(), ServiceBase(httpContext)));
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

    private static async Task<IFormCollection> ReadSearchFormAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken,
        params string[] allowedParameters)
    {
        const long maximumFormBytes = 4096;
        var contentType = httpContext.Request.ContentType;
        var parameterSeparator = contentType?.IndexOf(';') ?? -1;
        var mediaType = parameterSeparator >= 0
            ? contentType![..parameterSeparator].Trim()
            : contentType?.Trim();
        if (!string.Equals(
                mediaType,
                "application/x-www-form-urlencoded",
                StringComparison.OrdinalIgnoreCase))
            throw new PopulationDataException(
                PopulationErrorKind.InvalidPatientContext,
                "FHIR POST search requires application/x-www-form-urlencoded content.");
        if (httpContext.Request.ContentLength is > maximumFormBytes)
            throw new PopulationDataException(
                PopulationErrorKind.InvalidPatientContext,
                "The FHIR POST search form is too large.");

        IFormCollection form;
        try
        {
            form = await httpContext.Request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException exception)
        {
            throw new PopulationDataException(
                PopulationErrorKind.InvalidPatientContext,
                "The FHIR POST search form is invalid.",
                exception);
        }

        var parsedCharacterCount = form.Sum(field =>
            field.Key.Length + field.Value.Sum(value => value?.Length ?? 0));
        if (parsedCharacterCount > maximumFormBytes)
            throw new PopulationDataException(
                PopulationErrorKind.InvalidPatientContext,
                "The FHIR POST search form is too large.");
        if (form.Files.Count != 0 ||
            form.Keys.Any(key => !allowedParameters.Contains(key, StringComparer.Ordinal)))
            throw new PopulationDataException(
                PopulationErrorKind.InvalidPatientContext,
                "The FHIR POST search form contains an unsupported parameter.");
        return form;
    }

    private static string RequiredSingleValue(IFormCollection form, string name)
    {
        if (!form.TryGetValue(name, out var values) ||
            values.Count != 1 ||
            string.IsNullOrWhiteSpace(values[0]))
            throw new PopulationDataException(
                PopulationErrorKind.InvalidPatientContext,
                $"The {name} search parameter is required exactly once.");
        return values[0]!;
    }

    private static string? OptionalSingleValue(IFormCollection form, string name)
    {
        if (!form.TryGetValue(name, out var values)) return null;
        if (values.Count != 1 || string.IsNullOrWhiteSpace(values[0]))
            throw new PopulationDataException(
                PopulationErrorKind.InvalidPatientContext,
                $"The {name} search parameter must be supplied at most once with a value.");
        return values[0];
    }

    private static Uri ServiceBase(HttpContext context) =>
        new($"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}/");
}
