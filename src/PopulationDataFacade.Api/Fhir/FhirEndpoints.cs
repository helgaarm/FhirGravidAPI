using Hl7.Fhir.Model;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using PopulationDataFacade.Api.Security;
using PopulationDataFacade.Core;
using System.Globalization;

namespace PopulationDataFacade.Api.Fhir;

public static class FhirEndpoints
{
    private const long MaximumSearchFormBytes = 4096;

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

        group.MapGet("/CareTeam", SearchCareTeamsAsync)
            .WithName("SearchCareTeams")
            .Produces(StatusCodes.Status200OK, contentType: "application/fhir+json");

        group.MapPost("/Patient/_search", SearchPatientByIdentifierAsync)
            .WithMetadata(new RequestSizeLimitAttribute(MaximumSearchFormBytes))
            .WithName("SearchPatientByIdentifier")
            .WithDescription("FHIR POST search med NIN i form body. Krever HelseID utenfor lokal DevelopmentTestMode. NIN returneres aldri.")
            .Produces(StatusCodes.Status200OK, contentType: "application/fhir+json")
            .Produces(StatusCodes.Status400BadRequest, contentType: "application/fhir+json")
            .Produces(StatusCodes.Status404NotFound, contentType: "application/fhir+json");

        group.MapPost("/Observation/_search", SearchObservationsByPatientIdentifierAsync)
            .WithMetadata(new RequestSizeLimitAttribute(MaximumSearchFormBytes))
            .WithName("SearchObservationsByPatientIdentifier")
            .WithDescription("FHIR POST search med patient NIN i form body. Krever HelseID utenfor lokal DevelopmentTestMode. NIN returneres aldri.")
            .Produces(StatusCodes.Status200OK, contentType: "application/fhir+json")
            .Produces(StatusCodes.Status400BadRequest, contentType: "application/fhir+json")
            .Produces(StatusCodes.Status404NotFound, contentType: "application/fhir+json");

        group.MapPost("/Encounter/_search", SearchEncountersByPatientIdentifierAsync)
            .WithMetadata(new RequestSizeLimitAttribute(MaximumSearchFormBytes))
            .WithName("SearchEncountersByPatientIdentifier")
            .WithDescription("FHIR POST search med patient NIN i form body. Krever HelseID utenfor lokal DevelopmentTestMode. NIN returneres aldri.")
            .Produces(StatusCodes.Status200OK, contentType: "application/fhir+json")
            .Produces(StatusCodes.Status400BadRequest, contentType: "application/fhir+json")
            .Produces(StatusCodes.Status404NotFound, contentType: "application/fhir+json");

        group.MapPost("/CareTeam/_search", SearchCareTeamsByPatientIdentifierAsync)
            .WithMetadata(new RequestSizeLimitAttribute(MaximumSearchFormBytes))
            .WithName("SearchCareTeamsByPatientIdentifier")
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
        var form = await ReadSearchFormAsync(
            httpContext,
            cancellationToken,
            "patient.identifier",
            "code",
            "category",
            "date");
        var requestContext = contextFactory.CreateForNinSearch(
            httpContext,
            RequiredSingleValue(form, "patient.identifier"));
        var snapshot = await service.GetSnapshotAsync(requestContext, cancellationToken);
        var search = ParseObservationSearch(
            OptionalSingleValue(form, "code"),
            OptionalSingleValue(form, "category"),
            OptionalSingleValue(form, "date"));
        var resources = mapper.MapObservations(snapshot, search).Cast<Resource>();
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

    private static async Task<IResult> SearchCareTeamsByPatientIdentifierAsync(
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
        return FhirHttp.Result(mapper.SearchBundle(mapper.MapCareTeams(snapshot).Cast<Resource>(), ServiceBase(httpContext)));
    }

    private static async Task<IResult> GetPatientAsync(
        string id,
        HttpContext httpContext,
        PatientRequestContextFactory contextFactory,
        IPopulationDataService service,
        IFhirPopulationMapper mapper,
        CancellationToken cancellationToken)
    {
        var requestContext = contextFactory.CreateForPatientRead(httpContext);
        var snapshot = await service.GetSnapshotAsync(requestContext, cancellationToken);
        if (string.Equals(snapshot.Patient.LogicalId, id, StringComparison.Ordinal))
            return FhirHttp.Result(mapper.MapPatient(snapshot.Patient));

        var fetus = mapper.MapFetusPatients(snapshot)
            .SingleOrDefault(candidate => string.Equals(candidate.Id, id, StringComparison.Ordinal));
        if (fetus is null)
            throw new PopulationDataException(
                PopulationErrorKind.NotFound,
                "The requested patient was not found in this context.");

        return FhirHttp.Result(fetus);
    }

    private static async Task<IResult> SearchObservationsAsync(
        string? patient,
        string? code,
        string? category,
        string? date,
        HttpContext httpContext,
        PatientRequestContextFactory contextFactory,
        IPopulationDataService service,
        IFhirPopulationMapper mapper,
        CancellationToken cancellationToken)
    {
        var patientId = RequiredPatient(patient);
        var requestContext = contextFactory.Create(httpContext, patientId);
        var snapshot = await service.GetSnapshotAsync(requestContext, cancellationToken);
        var search = ParseObservationSearch(code, category, date);
        var resources = mapper.MapObservations(snapshot, search).Cast<Resource>();
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

    private static async Task<IResult> SearchCareTeamsAsync(
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
        return FhirHttp.Result(mapper.SearchBundle(mapper.MapCareTeams(snapshot).Cast<Resource>(), ServiceBase(httpContext)));
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

    private static PopulationObservationSearch ParseObservationSearch(
        string? code,
        string? category,
        string? date)
    {
        string? categorySystem = null;
        string? categoryCode = null;
        if (!string.IsNullOrWhiteSpace(category))
        {
            var separator = category.IndexOf('|');
            if (separator < 0)
            {
                categoryCode = category;
            }
            else
            {
                if (separator == 0 || separator == category.Length - 1)
                    throw new PopulationDataException(
                        PopulationErrorKind.InvalidPatientContext,
                        "Category must use either code or system|code token form.");
                categorySystem = category[..separator];
                categoryCode = category[(separator + 1)..];
            }
        }

        return new PopulationObservationSearch(
            ParseCode(code),
            categorySystem,
            categoryCode,
            ParseDate(date));
    }

    private static PopulationDateSearch? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var comparison = PopulationDateComparison.Equal;
        var dateValue = value;
        if (value.Length > 2)
        {
            comparison = value[..2] switch
            {
                "eq" => PopulationDateComparison.Equal,
                "ne" => PopulationDateComparison.NotEqual,
                "gt" => PopulationDateComparison.GreaterThan,
                "lt" => PopulationDateComparison.LessThan,
                "ge" => PopulationDateComparison.GreaterThanOrEqual,
                "le" => PopulationDateComparison.LessThanOrEqual,
                _ => comparison
            };
            if (value[..2] is "eq" or "ne" or "gt" or "lt" or "ge" or "le")
                dateValue = value[2..];
        }

        if (!DateOnly.TryParseExact(
                dateValue,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
            throw new PopulationDataException(
                PopulationErrorKind.InvalidPatientContext,
                "Date must use optional eq, ne, gt, lt, ge, or le prefix followed by yyyy-MM-dd.");
        return new PopulationDateSearch(comparison, date);
    }

    private static async Task<IFormCollection> ReadSearchFormAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken,
        params string[] allowedParameters)
    {
        var requestSizeFeature = httpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (requestSizeFeature is { IsReadOnly: false })
            requestSizeFeature.MaxRequestBodySize = MaximumSearchFormBytes;
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
        if (httpContext.Request.ContentLength is > MaximumSearchFormBytes)
            throw new PopulationDataException(
                PopulationErrorKind.InvalidPatientContext,
                "The FHIR POST search form is too large.");

        IFormCollection form;
        try
        {
            form = await httpContext.Request.ReadFormAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidDataException or BadHttpRequestException)
        {
            throw new PopulationDataException(
                PopulationErrorKind.InvalidPatientContext,
                "The FHIR POST search form is invalid.",
                exception);
        }

        var parsedCharacterCount = form.Sum(field =>
            field.Key.Length + field.Value.Sum(value => value?.Length ?? 0));
        if (parsedCharacterCount > MaximumSearchFormBytes)
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
