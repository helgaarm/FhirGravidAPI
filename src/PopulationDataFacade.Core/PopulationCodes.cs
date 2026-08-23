namespace PopulationDataFacade.Core;

public static class PopulationCodes
{
    public const string Ucum = "http://unitsofmeasure.org";
    public const string Loinc = "http://loinc.org";
    public const string SnomedCt = "http://snomed.info/sct";
    public const string Volven3303 = "urn:oid:2.16.578.1.12.4.1.1.3303";
    public const string Volven8534 = "urn:oid:2.16.578.1.12.4.1.1.8534";
    public const string Volven8536 = "urn:oid:2.16.578.1.12.4.1.1.8536";
    public const string Volven8537 = "urn:oid:2.16.578.1.12.4.1.1.8537";
    public const string Volven8340 = "urn:oid:2.16.578.1.12.4.1.1.8340";
    public const string Nlk = "urn:oid:2.16.578.1.12.4.1.1.7280";

    public static readonly PopulationCode DateLastPeriod = LoincCode("8665-2", "Last menstrual period start date");
    public static readonly PopulationCode DueDateLastPeriod = Snomed("289206005", "Estimated date of delivery from last period");
    public static readonly PopulationCode DueDateUltrasound = Snomed("738070007", "Estimated date of delivery from antenatal ultrasound scan");
    public static readonly PopulationCode CorrectedDueDate = TextOnly("Korrigert termindato");
    public static readonly PopulationCode NumberOfFetuses = Snomed("246435002", "Number of fetuses");
    public static readonly PopulationCode AssistedConception = Snomed("813541000000100", "svangerskap ved assistert befruktning");
    public static readonly PopulationCode PrenatalDiagnosticsInformationProvided = TextOnly("Gitt informasjon om fosterdiagnostikk");
    public static readonly PopulationCode BirthPreparationTalk = Snomed("702396006", "Childbirth education");
    public static readonly PopulationCode BreastfeedingGuidance = Snomed("243094003", "Breastfeeding education");

    public static readonly PopulationCode PreviousPregnancies = Snomed("246211005", "Number of previous pregnancies");
    public static readonly PopulationCode PreviousLiveBirths = LoincCode("11636-8", "Number of live births");
    public static readonly PopulationCode SpontaneousMiscarriages = Snomed("248989003", "Number of miscarriages");
    public static readonly PopulationCode StillBirths22Weeks = Snomed("252112002", "Number of stillbirths");
    public static readonly PopulationCode EctopicPregnancies = Snomed("440537001", "Number of ectopic pregnancies");
    public static readonly PopulationCode PreviousPregnanciesNote = TextOnly("Merknad om tidligere svangerskap");

    public static readonly PopulationCode NoKnownGeneticDisorders = TextOnly("Ingen kjente arvelige sykdommer");
    public static readonly PopulationCode ParentsAreRelatives = Snomed("842009", "Consanguinity");
    public static readonly PopulationCode OtherGeneticDisorder = TextOnly("Annen arvelig sykdom");
    public static readonly PopulationCode GeneticDisordersNote = TextOnly("Merknad om arvelige sykdommer");
    public static readonly PopulationCode HipDysplasiaFamilyHistory = TextOnly("Hofteleddsdysplasi i familien");

    public static readonly PopulationCode NothingParticularMedical = TextOnly("Ingenting spesielt");
    public static readonly PopulationCode KidneyOrUrinaryTractDisease = TextOnly("Nyre- og/eller urinveissykdom");
    public static readonly PopulationCode AllergyOrAsthma = TextOnly("Allergi og/eller astma");
    public static readonly PopulationCode GynecologicalConditionOrIntervention = TextOnly("Gynekologisk sykdom, inngrep og/eller operasjon");
    public static readonly PopulationCode OtherMedicalCondition = TextOnly("Annen tidligere eller nåværende sykdom");
    public static readonly PopulationCode MedicalConditionsNote = TextOnly("Merknader/annet om tidligere eller nåværende sykdom");

    public static readonly PopulationCode HeartDisease = Snomed("56265001", "Heart disease");
    public static readonly PopulationCode HypertensiveDisorder = Snomed("38341003", "Hypertensive disorder");
    public static readonly PopulationCode DiabetesMellitus = Snomed("73211009", "Diabetes mellitus");
    public static readonly PopulationCode Epilepsy = Snomed("84757009", "Epilepsy");
    public static readonly PopulationCode Thrombosis = Snomed("439127006", "Thrombosis");
    public static readonly PopulationCode AutoimmuneDisease = Snomed("85828009", "Autoimmune disease");
    public static readonly PopulationCode MentalDisorder = Snomed("74732009", "Mental disorder");

    public static readonly PopulationCode DrugAllergy = Snomed("416098002", "Allergy to drug");
    public static readonly PopulationCode FolateIntake = Snomed("792807003", "Folic acid intake");
    public static readonly PopulationCode MedicationFrequency = TextOnly("Hyppighet av legemiddelbruk");
    public static readonly PopulationCode MedicationNote = TextOnly("Merknad om legemiddelbruk");
    public static readonly PopulationCode DailyStimulusCount = TextOnly("Daglig antall");

    public static readonly PopulationCode AboType = NlkCode("NPU58582", "Ery-ABO-fenotype");
    public static readonly PopulationCode RhesusDType = NlkCode("NPU21917", "Ery-Rh-D-antigen");
    public static readonly PopulationCode Hemoglobin = NlkCode("NOR05172", "B-Hemoglobin");
    public static readonly PopulationCode Ferritin = NlkCode("NPU19763", "P-Ferritin");
    public static readonly PopulationCode Hbv = Snomed("165806002", "Hepatitis B surface antigen detected");
    public static readonly PopulationCode HbvCoreAntibodyTestResult = TextOnly("P-Hepatitt B-virus (HBV) core-antistoff");
    public static readonly PopulationCode BloodTypeAntibodyTestResult = TextOnly("Blodtypeantistoffer");
    public static readonly PopulationCode HivTestResult = TextOnly("Prøveresultat for HIV");
    public static readonly PopulationCode SyphilisTestResult = TextOnly("Prøveresultat for syfilis");
    public static readonly PopulationCode ChlamydiaTestResult = TextOnly("Prøveresultat for klamydia");
    public static readonly PopulationCode ToxoplasmosisTestResult = TextOnly("Prøveresultat for toksoplasmose");
    public static readonly PopulationCode RubellaIgg = NlkCode("NPU12412", "P-Rubellavirus IgG");
    public static readonly PopulationCode HepatitisCTestResult = TextOnly("Prøveresultat for hepatitt C");
    public static readonly PopulationCode MrsaVreEsblTestResult = TextOnly("Prøveresultat for MRSA, VRE og/eller ESBL");
    public static readonly PopulationCode GonorrheaTestResult = TextOnly("Prøveresultat for gonoré");
    public static readonly PopulationCode CytomegalovirusTestResult = TextOnly("Prøveresultat for cytomegalovirus");
    public static readonly PopulationCode AsymptomaticBacteriuriaTestResult = TextOnly("Prøveresultat for asymptomatisk bakteriuri");
    public static readonly PopulationCode GroupBStreptococciTestResult = TextOnly("Prøveresultat for gruppe B-streptokokker");
    public static readonly PopulationCode ClinicalTestsNote = TextOnly("Merknad om kliniske prøver");
    public static readonly PopulationCode HbA1c = NlkCode("NPU27300", "B-HbA1c");
    public static readonly PopulationCode GlucoseFasting = Snomed("271062006", "Fasting blood glucose measurement");
    public static readonly PopulationCode Glucose2Hour = Snomed("49167009", "Measurement of glucose 2 hours after glucose challenge for glucose tolerance test");

    public static readonly PopulationCode RhesusProphylaxis = Snomed("408783007", "Antenatal anti-D prophylaxis status");

    public static readonly PopulationCode SymphysisFundalHeight = Snomed("364253002", "Fundal height of uterus");
    public static readonly PopulationCode GestationalAge = LoincCode("18185-9", "Gestational age");
    public static readonly PopulationCode BodyHeight = Snomed("50373000", "Body height measure");
    public static readonly PopulationCode MotherWeight = Snomed("27113001", "Body weight");
    public static readonly PopulationCode BodyMassIndex = Snomed("60621009", "Body mass index");
    public static readonly PopulationCode BloodPressure = LoincCode("85354-9", "Blood pressure panel with all children optional");
    public static readonly PopulationCode Systolic = Snomed("4471000202106", "Systemic systolic arterial blood pressure");
    public static readonly PopulationCode Diastolic = Snomed("4481000202108", "Systemic diastolic arterial blood pressure");
    public static readonly PopulationCode UrineProtein = NlkCode("NPU04206", "Protein in urine");
    public static readonly PopulationCode EdemaGrade = TextOnly("Ødemgrad (DHG-verdi 0–3)");
    public static readonly PopulationCode AntenatalMedicationReported = TextOnly("Legemiddelbruk registrert ved svangerskapskontroll");
    public static readonly PopulationCode AntenatalAppointmentNote = TextOnly("Merknad fra svangerskapskontroll");

    public static readonly PopulationCode CohabitingCoparent = TextOnly("Bor sammen med medforelder");
    public static readonly PopulationCode CohabitingCoparentNote = TextOnly("Merknad om boforhold med medforelder");

    public static readonly PopulationCode FetalHeartRate = Snomed("364075005", "Heart rate");
    public static readonly PopulationCode FetalPresentationLie = TextOnly("Fosterleie og -presentasjon");
    public static readonly PopulationCode FetalMovementsReported = LoincCode("57088-7", "Fetal Movement - Reported");
    public static readonly PopulationCode FetalFindingsNote = TextOnly("Merknad om fosterfunn");

    private static readonly IReadOnlyDictionary<(string System, string Code), PopulationCode[]> SupplementalCodings =
        new Dictionary<(string System, string Code), PopulationCode[]>
        {
            [(DueDateLastPeriod.System!, DueDateLastPeriod.Code!)] = [LoincCode("11778-8", "Delivery date Estimated")],
            [(DueDateUltrasound.System!, DueDateUltrasound.Code!)] = [LoincCode("11778-8", "Delivery date Estimated")],
            [(AboType.System!, AboType.Code!)] = [LoincCode("883-9", "ABO group [Type] in Blood")],
            [(RhesusDType.System!, RhesusDType.Code!)] = [LoincCode("10331-7", "Rh [Type] in Blood")],
            [(BodyHeight.System!, BodyHeight.Code!)] = [LoincCode("8302-2", "Body height")],
            [(MotherWeight.System!, MotherWeight.Code!)] = [LoincCode("29463-7", "Body weight")],
            [(BodyMassIndex.System!, BodyMassIndex.Code!)] = [LoincCode("39156-5", "Body mass index (BMI) [Ratio]")],
            [(Systolic.System!, Systolic.Code!)] = [LoincCode("8480-6", "Systolic blood pressure")],
            [(Diastolic.System!, Diastolic.Code!)] = [LoincCode("8462-4", "Diastolic blood pressure")],
            [(FetalHeartRate.System!, FetalHeartRate.Code!)] = [LoincCode("55283-6", "Fetal Heart rate")]
        };

    public static IEnumerable<PopulationCode> CodingsFor(PopulationCode code)
    {
        if (!code.HasCoding) yield break;

        yield return code;
        if (!SupplementalCodings.TryGetValue((code.System!, code.Code!), out var supplementalCodings))
            yield break;

        foreach (var supplementalCoding in supplementalCodings)
            yield return supplementalCoding;
    }

    public static bool Matches(PopulationCode source, PopulationCode filter) =>
        !filter.HasCoding
            ? source == filter
            : CodingsFor(source).Any(coding =>
                string.Equals(coding.System, filter.System, StringComparison.Ordinal) &&
                string.Equals(coding.Code, filter.Code, StringComparison.Ordinal));

    public static PopulationCode Lifestyle(string code, string display) => new(Volven8536, code, display);
    private static PopulationCode LoincCode(string code, string display) => new(Loinc, code, display);
    private static PopulationCode NlkCode(string code, string display) => new(Nlk, code, display);
    private static PopulationCode Snomed(string code, string display) => new(SnomedCt, code, display);
    private static PopulationCode TextOnly(string display) => new(null, null, display);
}
