using System.Text.Json;
using System.Text.Json.Serialization;

namespace PopulationDataFacade.Infrastructure.Dhg;

public abstract class DhgExtensible
{
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

public sealed class DhgMaternityRecord : DhgExtensible
{
    [JsonPropertyName("metadata")] public DhgRecordMetadata? Metadata { get; set; }
    [JsonPropertyName("antenatalAppointments")] public List<DhgAntenatalAppointment>? AntenatalAppointments { get; set; }
    [JsonPropertyName("birthStatus")] public DhgBirthStatusResource? BirthStatus { get; set; }
    [JsonPropertyName("clinicalTests")] public DhgClinicalTests? ClinicalTests { get; set; }
    [JsonPropertyName("currentPregnancy")] public DhgCurrentPregnancy? CurrentPregnancy { get; set; }
    [JsonPropertyName("geneticDisorders")] public DhgGeneticDisorders? GeneticDisorders { get; set; }
    [JsonPropertyName("lifestyleFactors")] public DhgLifestyleFactors? LifestyleFactors { get; set; }
    [JsonPropertyName("medicalConditions")] public DhgMedicalConditions? MedicalConditions { get; set; }
    [JsonPropertyName("medication")] public DhgMedication? Medication { get; set; }
    [JsonPropertyName("mother")] public DhgMother? Mother { get; set; }
    [JsonPropertyName("pointsOfContact")] public DhgPointsOfContact? PointsOfContact { get; set; }
    [JsonPropertyName("previousPregnancies")] public DhgPreviousPregnancies? PreviousPregnancies { get; set; }
    [JsonPropertyName("rhesusDNegative")] public DhgRhesusDNegative? RhesusDNegative { get; set; }
    [JsonPropertyName("symphysisFundalHeights")] public List<DhgSymphysisFundalHeight>? SymphysisFundalHeights { get; set; }
    [JsonPropertyName("vitalMeasurementsBeforePregnancy")] public DhgVitalMeasurementsBeforePregnancy? VitalMeasurementsBeforePregnancy { get; set; }
}

public sealed class DhgStatusResponse : DhgExtensible
{
    [JsonPropertyName("hasGivenConsent")] public bool? HasGivenConsent { get; set; }
    [JsonPropertyName("hasActiveMaternityRecord")] public bool? HasActiveMaternityRecord { get; set; }
    [JsonPropertyName("lastChangedDateTime")] public DateTimeOffset? LastChangedDateTime { get; set; }
    [JsonPropertyName("latestRecordId")] public string? LatestRecordId { get; set; }
    [JsonPropertyName("deceased")] public bool? Deceased { get; set; }
}

public sealed class DhgResourceMetadata : DhgExtensible
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("version")] public int? Version { get; set; }
    [JsonPropertyName("lastUpdated")] public DateTimeOffset? LastUpdated { get; set; }
    [JsonPropertyName("enteredInError")] public bool? EnteredInError { get; set; }
    [JsonPropertyName("lastUpdatedBy")] public DhgLastUpdatedBy? LastUpdatedBy { get; set; }
}

public sealed class DhgRecordMetadata : DhgExtensible
{
    [JsonPropertyName("recordId")] public string? RecordId { get; set; }
    [JsonPropertyName("recordStatus")] public DhgRecordStatus? RecordStatus { get; set; }
    [JsonPropertyName("version")] public int? Version { get; set; }
    [JsonPropertyName("recordLastUpdated")] public DateTimeOffset? RecordLastUpdated { get; set; }
    [JsonPropertyName("lastUpdated")] public DateTimeOffset? LastUpdated { get; set; }
    [JsonPropertyName("lastUpdatedBy")] public DhgLastUpdatedBy? LastUpdatedBy { get; set; }
}

public sealed class DhgLastUpdatedBy : DhgExtensible
{
    [JsonPropertyName("userType")] public string? UserType { get; set; }
    [JsonPropertyName("orgNr")] public string? OrganizationNumber { get; set; }
    [JsonPropertyName("orgName")] public string? OrganizationName { get; set; }
    [JsonPropertyName("treatmentFacilityName")] public string? TreatmentFacilityName { get; set; }
    [JsonPropertyName("hprNr")] public string? HprNumber { get; set; }
    [JsonPropertyName("hprRole")] public string? HprRole { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
}

public sealed class DhgRecordStatus : DhgExtensible
{
    [JsonPropertyName("status")] public string? Status { get; set; }
    [JsonPropertyName("deliveryDate")] public DateTimeOffset? DeliveryDate { get; set; }
    [JsonPropertyName("liveBirth")] public bool? LiveBirth { get; set; }
    [JsonPropertyName("terminationDate")] public DateTimeOffset? TerminationDate { get; set; }
}

public sealed class DhgCodeAndSystem : DhgExtensible
{
    [JsonPropertyName("code")] public string? Code { get; set; }
    [JsonPropertyName("display")] public string? Display { get; set; }
    [JsonPropertyName("codeSystem")] public string? CodeSystem { get; set; }
}

public sealed class DhgAntenatalAppointment : DhgExtensible
{
    [JsonPropertyName("metadata")] public DhgResourceMetadata? Metadata { get; set; }
    [JsonPropertyName("appointmentDate")] public DateOnly? AppointmentDate { get; set; }
    [JsonPropertyName("pregnancyWeek")] public int? PregnancyWeek { get; set; }
    [JsonPropertyName("daysAfterFullPregnancyWeek")] public int? DaysAfterFullPregnancyWeek { get; set; }
    [JsonPropertyName("motherWeight")] public decimal? MotherWeight { get; set; }
    [JsonPropertyName("bloodPressure")] public string? BloodPressure { get; set; }
    [JsonPropertyName("proteinInUrineTestResult")] public string? ProteinInUrineTestResult { get; set; }
    [JsonPropertyName("edema")] public int? Edema { get; set; }
    [JsonPropertyName("fetusesVitalSigns")] public List<DhgFetusVitalSigns>? FetusesVitalSigns { get; set; }
    [JsonPropertyName("medication")] public bool? Medication { get; set; }
    [JsonPropertyName("employmentRate")] public int? EmploymentRate { get; set; }
    [JsonPropertyName("note")] public string? Note { get; set; }
}

public sealed class DhgFetusVitalSigns : DhgExtensible
{
    [JsonPropertyName("fosterId")] public int? FetusId { get; set; }
    [JsonPropertyName("fetalHeartRate")] public int? FetalHeartRate { get; set; }
    [JsonPropertyName("fetalPresentationLie")] public DhgCodeAndSystem? FetalPresentationLie { get; set; }
    [JsonPropertyName("motherFeelsBabyMovements")] public bool? MotherFeelsBabyMovements { get; set; }
    [JsonPropertyName("note")] public string? Note { get; set; }
}

public sealed class DhgBirthStatusResource : DhgExtensible
{
    [JsonPropertyName("metadata")] public DhgResourceMetadata? Metadata { get; set; }
    [JsonPropertyName("birthStatus")] public List<DhgBirthStatusEntry>? BirthStatus { get; set; }
}

public sealed class DhgBirthStatusEntry : DhgExtensible
{
    [JsonPropertyName("fosterId")] public int? FetusId { get; set; }
    [JsonPropertyName("status")] public DhgCodeAndSystem? Status { get; set; }
    [JsonPropertyName("datetime")] public DateTimeOffset? DateTime { get; set; }
}

public sealed class DhgClinicalTests : DhgExtensible
{
    [JsonPropertyName("metadata")] public DhgResourceMetadata? Metadata { get; set; }
    [JsonPropertyName("hemoglobin")] public decimal? Hemoglobin { get; set; }
    [JsonPropertyName("hemoglobinAt3rdTrimester")] public decimal? HemoglobinAtThirdTrimester { get; set; }
    [JsonPropertyName("ferritin")] public decimal? Ferritin { get; set; }
    [JsonPropertyName("hbv")] public bool? Hbv { get; set; }
    [JsonPropertyName("hbvCore")] public bool? HbvCore { get; set; }
    [JsonPropertyName("hiv")] public bool? Hiv { get; set; }
    [JsonPropertyName("syphilis")] public bool? Syphilis { get; set; }
    [JsonPropertyName("aboRh")] public DhgAboRh? AboRh { get; set; }
    [JsonPropertyName("bloodAntibodies")] public bool? BloodAntibodies { get; set; }
    [JsonPropertyName("chlamydia")] public bool? Chlamydia { get; set; }
    [JsonPropertyName("toxoplasmosis")] public bool? Toxoplasmosis { get; set; }
    [JsonPropertyName("rubellaAntigen")] public bool? RubellaAntigen { get; set; }
    [JsonPropertyName("hepatitisC")] public bool? HepatitisC { get; set; }
    [JsonPropertyName("mrsaVreEsbl")] public bool? MrsaVreEsbl { get; set; }
    [JsonPropertyName("bHbA1c")] public int? BHbA1c { get; set; }
    [JsonPropertyName("glucoseTolerance")] public DhgGlucoseTolerance? GlucoseTolerance { get; set; }
    [JsonPropertyName("gonorrhea")] public bool? Gonorrhea { get; set; }
    [JsonPropertyName("cytomegaloVirus")] public bool? CytomegaloVirus { get; set; }
    [JsonPropertyName("asymptomaticBacteriuria")] public bool? AsymptomaticBacteriuria { get; set; }
    [JsonPropertyName("groupBStreptococci")] public bool? GroupBStreptococci { get; set; }
    [JsonPropertyName("note")] public string? Note { get; set; }
}

public sealed class DhgAboRh : DhgExtensible
{
    [JsonPropertyName("aboType")] public string? AboType { get; set; }
    [JsonPropertyName("rhesusDType")] public string? RhesusDType { get; set; }
}

public sealed class DhgGlucoseTolerance : DhgExtensible
{
    [JsonPropertyName("fastingGlucoseLevel")] public decimal? FastingGlucoseLevel { get; set; }
    [JsonPropertyName("post2hGlucoseLevel")] public decimal? PostTwoHourGlucoseLevel { get; set; }
    [JsonPropertyName("testDate")] public DateOnly? TestDate { get; set; }
}

public sealed class DhgCurrentPregnancy : DhgExtensible
{
    [JsonPropertyName("metadata")] public DhgResourceMetadata? Metadata { get; set; }
    [JsonPropertyName("dateLastPeriod")] public DateOnly? DateLastPeriod { get; set; }
    [JsonPropertyName("dueDate")] public DateOnly? DueDate { get; set; }
    [JsonPropertyName("dueDateBasedOnUltrasound")] public DateOnly? DueDateBasedOnUltrasound { get; set; }
    [JsonPropertyName("dueDateCorrectedDate")] public DateOnly? DueDateCorrectedDate { get; set; }
    [JsonPropertyName("hasPrenatalDiagnosticsTests")] public bool? HasPrenatalDiagnosticsTests { get; set; }
    [JsonPropertyName("numberOfFetuses")] public int? NumberOfFetuses { get; set; }
    [JsonPropertyName("assistedConception")] public DhgAssistedConception? AssistedConception { get; set; }
    [JsonPropertyName("birthPreparationTalk")] public bool? BirthPreparationTalk { get; set; }
    [JsonPropertyName("breastfeedingGuidance")] public bool? BreastfeedingGuidance { get; set; }
}

public sealed class DhgAssistedConception : DhgExtensible
{
    [JsonPropertyName("hadAssistedConception")] public bool? HadAssistedConception { get; set; }
    [JsonPropertyName("dateAssistedConception")] public DateOnly? DateAssistedConception { get; set; }
}

public sealed class DhgGeneticDisorders : DhgExtensible
{
    [JsonPropertyName("metadata")] public DhgResourceMetadata? Metadata { get; set; }
    [JsonPropertyName("noneKnown")] public bool? NoneKnown { get; set; }
    [JsonPropertyName("parentsAreRelatives")] public bool? ParentsAreRelatives { get; set; }
    [JsonPropertyName("hipDysplasia")] public bool? HipDysplasia { get; set; }
    [JsonPropertyName("other")] public bool? Other { get; set; }
    [JsonPropertyName("note")] public string? Note { get; set; }
}

public sealed class DhgLifestyleFactors : DhgExtensible
{
    [JsonPropertyName("metadata")] public DhgResourceMetadata? Metadata { get; set; }
    [JsonPropertyName("stimuli")] public List<DhgStimulus>? Stimuli { get; set; }
    [JsonPropertyName("note")] public string? Note { get; set; }
}

public sealed class DhgStimulus : DhgExtensible
{
    [JsonPropertyName("stimuliType")] public DhgCodeAndSystem? StimuliType { get; set; }
    [JsonPropertyName("stimuliFrequencyFirstConsultation")] public DhgStimuliFrequency? FirstConsultation { get; set; }
    [JsonPropertyName("stimuliFrequencyAtWeek36")] public DhgStimuliFrequency? AtWeek36 { get; set; }
}

public sealed class DhgStimuliFrequency : DhgExtensible
{
    [JsonPropertyName("stimuliFrequency")] public DhgCodeAndSystem? Frequency { get; set; }
    [JsonPropertyName("dailyCount")] public int? DailyCount { get; set; }
}

public sealed class DhgMedicalConditions : DhgExtensible
{
    [JsonPropertyName("metadata")] public DhgResourceMetadata? Metadata { get; set; }
    [JsonPropertyName("nothingParticular")] public bool? NothingParticular { get; set; }
    [JsonPropertyName("heartDisease")] public bool? HeartDisease { get; set; }
    [JsonPropertyName("highBloodPressure")] public bool? HighBloodPressure { get; set; }
    [JsonPropertyName("kidneyUrinaryTractDiseases")] public bool? KidneyUrinaryTractDiseases { get; set; }
    [JsonPropertyName("diabetes")] public bool? Diabetes { get; set; }
    [JsonPropertyName("allergiesAsthma")] public bool? AllergiesAsthma { get; set; }
    [JsonPropertyName("epilepsy")] public bool? Epilepsy { get; set; }
    [JsonPropertyName("thrombosis")] public bool? Thrombosis { get; set; }
    [JsonPropertyName("autoimmuneDisease")] public bool? AutoimmuneDisease { get; set; }
    [JsonPropertyName("gynecologicalConditions")] public bool? GynecologicalConditions { get; set; }
    [JsonPropertyName("mentalHealth")] public bool? MentalHealth { get; set; }
    [JsonPropertyName("other")] public bool? Other { get; set; }
    [JsonPropertyName("note")] public string? Note { get; set; }
}

public sealed class DhgMedication : DhgExtensible
{
    [JsonPropertyName("metadata")] public DhgResourceMetadata? Metadata { get; set; }
    [JsonPropertyName("medicationFrequency")] public string? MedicationFrequency { get; set; }
    [JsonPropertyName("drugAllergy")] public bool? DrugAllergy { get; set; }
    [JsonPropertyName("folate")] public DhgFolate? Folate { get; set; }
    [JsonPropertyName("note")] public string? Note { get; set; }
}

public sealed class DhgFolate : DhgExtensible
{
    [JsonPropertyName("takenBefore")] public bool? TakenBefore { get; set; }
    [JsonPropertyName("takenDuring")] public bool? TakenDuring { get; set; }
}

public sealed class DhgMother : DhgExtensible
{
    [JsonPropertyName("metadata")] public DhgResourceMetadata? Metadata { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("address")] public string? Address { get; set; }
    [JsonPropertyName("postNumber")] public string? PostNumber { get; set; }
    [JsonPropertyName("postName")] public string? PostName { get; set; }
    [JsonPropertyName("employedLast6Months")] public bool? EmployedLastSixMonths { get; set; }
    [JsonPropertyName("employmentPercentage")] public int? EmploymentPercentage { get; set; }
    [JsonPropertyName("occupationAndIndustry")] public string? OccupationAndIndustry { get; set; }
    [JsonPropertyName("language")] public DhgCodeAndSystem? Language { get; set; }
    [JsonPropertyName("countryOfBirth")] public DhgCodeAndSystem? CountryOfBirth { get; set; }
    [JsonPropertyName("needsLanguageInterpreter")] public bool? NeedsLanguageInterpreter { get; set; }
    [JsonPropertyName("cohabitingCoparent")] public bool? CohabitingCoparent { get; set; }
    [JsonPropertyName("cohabitingCoparentNote")] public string? CohabitingCoparentNote { get; set; }
}

public sealed class DhgPointsOfContact : DhgExtensible
{
    [JsonPropertyName("metadata")] public DhgResourceMetadata? Metadata { get; set; }
    [JsonPropertyName("generalPractitioner")] public DhgPersonAndOrganization? GeneralPractitioner { get; set; }
    [JsonPropertyName("midwife")] public DhgPersonAndOrganization? Midwife { get; set; }
    [JsonPropertyName("birthInstitute")] public string? BirthInstitute { get; set; }
    [JsonPropertyName("maternityHealthcareCentre")] public string? MaternityHealthcareCentre { get; set; }
}

public sealed class DhgPersonAndOrganization : DhgExtensible
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("organizationName")] public string? OrganizationName { get; set; }
    [JsonPropertyName("organizationId")] public string? OrganizationId { get; set; }
    [JsonPropertyName("hprNr")] public string? HprNumber { get; set; }
}

public sealed class DhgPreviousPregnancies : DhgExtensible
{
    [JsonPropertyName("metadata")] public DhgResourceMetadata? Metadata { get; set; }
    [JsonPropertyName("numberOfPreviousPregnancies")] public int? NumberOfPreviousPregnancies { get; set; }
    [JsonPropertyName("numberOfPreviousLiveBirths")] public int? NumberOfPreviousLiveBirths { get; set; }
    [JsonPropertyName("spontaneousMiscarriages")] public int? SpontaneousMiscarriages { get; set; }
    [JsonPropertyName("stillBirths22weeks")] public int? StillBirths22Weeks { get; set; }
    [JsonPropertyName("numberOfEctopicPregnancies")] public int? NumberOfEctopicPregnancies { get; set; }
    [JsonPropertyName("note")] public string? Note { get; set; }
}

public sealed class DhgRhesusDNegative : DhgExtensible
{
    [JsonPropertyName("metadata")] public DhgResourceMetadata? Metadata { get; set; }
    [JsonPropertyName("consentFetalRhesusTyping")] public bool? ConsentFetalRhesusTyping { get; set; }
    [JsonPropertyName("fetusRhDPositiveAtWeek24")] public bool? FetusRhDPositiveAtWeek24 { get; set; }
    [JsonPropertyName("prophylaxisAtWeek28")] public bool? ProphylaxisAtWeek28 { get; set; }
    [JsonPropertyName("dateForResult")] public DateOnly? DateForResult { get; set; }
    [JsonPropertyName("note")] public string? Note { get; set; }
}

public sealed class DhgSymphysisFundalHeight : DhgExtensible
{
    [JsonPropertyName("metadata")] public DhgResourceMetadata? Metadata { get; set; }
    [JsonPropertyName("pregnancyWeek")] public int? PregnancyWeek { get; set; }
    [JsonPropertyName("measurement")] public int? Measurement { get; set; }
    [JsonPropertyName("measurementDate")] public DateOnly? MeasurementDate { get; set; }
}

public sealed class DhgVitalMeasurementsBeforePregnancy : DhgExtensible
{
    [JsonPropertyName("metadata")] public DhgResourceMetadata? Metadata { get; set; }
    [JsonPropertyName("height")] public decimal? Height { get; set; }
    [JsonPropertyName("prePregnancyWeight")] public decimal? PrePregnancyWeight { get; set; }
    [JsonPropertyName("bMI")] public decimal? BMI { get; set; }
}
