namespace PopulationDataFacade.Core;

public sealed record PatientRequestContext(
    string LogicalId,
    string NationalIdentityNumber,
    string SubjectToken,
    string CorrelationId);

public sealed record PopulationSnapshot(
    PopulationPatient Patient,
    IReadOnlyList<PopulationObservation> Observations,
    IReadOnlyList<PopulationEncounter> Encounters,
    DateTimeOffset? SourceLastChanged,
    bool HasActiveMaternityRecord,
    IReadOnlyList<PopulationCareTeam>? CareTeams = null,
    IReadOnlyList<PopulationFetusPatient>? Fetuses = null);

public sealed record PopulationPatient(
    string LogicalId,
    CodedValue? PreferredLanguage,
    bool? NeedsInterpreter,
    DateTimeOffset? LastUpdated);

public sealed record PopulationFetusPatient(
    string LogicalId,
    DateTimeOffset? LastUpdated);

public sealed record PopulationObservation(
    string Id,
    PopulationCode Code,
    PopulationValue? Value,
    string Category,
    DateTimeOffset? LastUpdated,
    PopulationEffective? Effective = null,
    IReadOnlyList<PopulationComponent>? Components = null,
    string? EncounterId = null,
    string? Note = null,
    string? FocusPatientId = null);

public sealed record PopulationObservationSearch(
    PopulationCode? Code = null,
    string? CategorySystem = null,
    string? CategoryCode = null,
    PopulationDateSearch? Date = null);

public sealed record PopulationDateSearch(PopulationDateComparison Comparison, DateOnly Value);

public enum PopulationDateComparison
{
    Equal,
    NotEqual,
    GreaterThan,
    LessThan,
    GreaterThanOrEqual,
    LessThanOrEqual
}

public sealed record PopulationComponent(PopulationCode Code, PopulationValue Value);

public sealed record PopulationEncounter(
    string Id,
    DateOnly Date,
    DateTimeOffset? LastUpdated);

public sealed record PopulationCareTeam(
    string Id,
    PopulationCareTeamMember? Midwife,
    string? MaternityHealthcareCentre,
    DateTimeOffset? LastUpdated);

public sealed record PopulationCareTeamMember(
    string? Name,
    string? OrganizationName);

public sealed record PopulationCode(string? System, string? Code, string Display)
{
    public bool HasCoding =>
        !string.IsNullOrWhiteSpace(System) &&
        !string.IsNullOrWhiteSpace(Code);
}

public abstract record PopulationValue;
public sealed record BooleanValue(bool Value) : PopulationValue;
public sealed record IntegerValue(int Value) : PopulationValue;
public sealed record DecimalValue(decimal Value) : PopulationValue;
public sealed record DateValue(DateOnly Value) : PopulationValue;
public sealed record DateTimeValue(DateTimeOffset Value) : PopulationValue;
public sealed record TextValue(string Value) : PopulationValue;
public sealed record CodedValue(string System, string Code, string? Display) : PopulationValue;
public sealed record QuantityValue(decimal Value, string Unit, string System, string Code) : PopulationValue;

public abstract record PopulationEffective;
public sealed record EffectiveDate(DateOnly Value) : PopulationEffective;
public sealed record EffectiveDateTime(DateTimeOffset Value) : PopulationEffective;

public interface IPopulationDataService
{
    Task<PopulationSnapshot> GetSnapshotAsync(PatientRequestContext context, CancellationToken cancellationToken);
}

public sealed class PopulationDataException : Exception
{
    public PopulationDataException(PopulationErrorKind kind, string message, Exception? innerException = null)
        : base(message, innerException) => Kind = kind;

    public PopulationErrorKind Kind { get; }
}

public enum PopulationErrorKind
{
    InvalidPatientContext,
    ConsentMissing,
    NoActiveMaternityRecord,
    Unauthorized,
    Forbidden,
    NotFound,
    RateLimited,
    SourceUnavailable,
    SourceContractInvalid,
    ConfigurationInvalid
}
