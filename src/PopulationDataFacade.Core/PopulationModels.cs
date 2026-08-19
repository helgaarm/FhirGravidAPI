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
    bool HasActiveMaternityRecord);

public sealed record PopulationPatient(
    string LogicalId,
    CodedValue? PreferredLanguage,
    bool? NeedsInterpreter,
    DateTimeOffset? LastUpdated);

public sealed record PopulationObservation(
    string Id,
    PopulationCode Code,
    PopulationValue Value,
    string Category,
    DateTimeOffset? LastUpdated,
    PopulationEffective? Effective = null,
    IReadOnlyList<PopulationComponent>? Components = null,
    string? EncounterId = null,
    string? Note = null);

public sealed record PopulationComponent(PopulationCode Code, PopulationValue Value);

public sealed record PopulationEncounter(
    string Id,
    DateOnly Date,
    DateTimeOffset? LastUpdated);

public sealed record PopulationCode(string System, string Code, string Display);

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
