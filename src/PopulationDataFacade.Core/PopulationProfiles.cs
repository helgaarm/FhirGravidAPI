namespace PopulationDataFacade.Core;

public static class PopulationProfiles
{
    public const string NorwegianVitalSignsPackage = "hl7.fhir.no.domain.vitalsigns";
    public const string NorwegianVitalSignsPackageVersion = "0.9.74";
    public const string NorwegianBasisPackage = "hl7.fhir.no.basis";
    public const string NorwegianBasisPackageVersion = "2.2.2";

    public const string NorwegianVitalSignsBloodPressure =
        "http://hl7.no/fhir/no-domain/vitalsigns/StructureDefinition/no-domain-VitalSigns-Observation-bloodpressure";

    public const string NorwegianVitalSignsBodyWeight =
        "http://hl7.no/fhir/no-domain/vitalsigns/StructureDefinition/no-domain-VitalSigns-Observation-bodyweight";

    // Reference only. This facade cannot claim conformance until it can supply the
    // mandatory Nilar diagnostic-report reference and the report-specific semantics.
    public const string NilarObservation =
        "http://nhn.no/fhir/nilar/StructureDefinition/nilar-observation";
}
