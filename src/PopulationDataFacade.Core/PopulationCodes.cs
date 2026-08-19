namespace PopulationDataFacade.Core;

public static class PopulationCodes
{
    public const string System = "urn:nhn:population-data";
    public const string Ucum = "http://unitsofmeasure.org";
    public const string Volven3303 = "urn:oid:2.16.578.1.12.4.1.1.3303";
    public const string Volven8534 = "urn:oid:2.16.578.1.12.4.1.1.8534";
    public const string Volven8536 = "urn:oid:2.16.578.1.12.4.1.1.8536";
    public const string Volven8537 = "urn:oid:2.16.578.1.12.4.1.1.8537";
    public const string Volven8340 = "urn:oid:2.16.578.1.12.4.1.1.8340";
    public const string Nlk = "urn:oid:2.16.578.1.12.4.1.1.7280";

    public static readonly PopulationCode NeedsInterpreter = Local("needs-language-interpreter", "Behov for tolk");
    public static readonly PopulationCode DueDateLastPeriod = Local("due-date-last-period", "Beregnet termin basert på siste menstruasjon");
    public static readonly PopulationCode DueDateUltrasound = Local("due-date-ultrasound", "Ultralydtermin");
    public static readonly PopulationCode DateLastPeriod = Local("date-last-period", "Dato for siste menstruasjon");
    public static readonly PopulationCode NumberOfFetuses = Local("number-of-fetuses", "Antall fostre");
    public static readonly PopulationCode AssistedConception = Local("assisted-conception", "Assistert befruktning");
    public static readonly PopulationCode AssistedConceptionDate = Local("assisted-conception-date", "Dato for assistert befruktning");
    public static readonly PopulationCode RecordedGestationalAge = Local("recorded-gestational-age", "Sist registrerte gestasjonsalder");
    public static readonly PopulationCode GestationalAgeAtAppointment = Local("gestational-age-at-appointment", "Gestasjonsalder ved konsultasjon");
    public static readonly PopulationCode GestationalWeeks = Local("gestational-weeks", "Fullgåtte svangerskapsuker");
    public static readonly PopulationCode GestationalDays = Local("gestational-days", "Dager etter fullgått svangerskapsuke");

    public static readonly PopulationCode PreviousPregnancies = Local("previous-pregnancies", "Antall tidligere graviditeter");
    public static readonly PopulationCode PreviousLiveBirths = Local("previous-live-births", "Antall tidligere levendefødte");
    public static readonly PopulationCode SpontaneousMiscarriages = Local("spontaneous-miscarriages", "Spontanaborter");
    public static readonly PopulationCode StillBirths22Weeks = Local("stillbirths-22-weeks", "Dødfødsler fra 22 uker / 500 g");
    public static readonly PopulationCode EctopicPregnancies = Local("ectopic-pregnancies", "Ektopiske graviditeter");
    public static readonly PopulationCode PreviousPregnancyNote = Local("previous-pregnancy-note", "Merknad om tidligere svangerskap");

    public static readonly PopulationCode GeneticNoneKnown = Local("genetic-none-known", "Ingen kjente arvelige forhold");
    public static readonly PopulationCode ParentsAreRelatives = Local("parents-are-relatives", "Foreldre er i slekt");
    public static readonly PopulationCode HipDysplasia = Local("hip-dysplasia", "Hofteleddsdysplasi");
    public static readonly PopulationCode OtherGeneticDisorder = Local("other-genetic-disorder", "Annen arvelig tilstand");
    public static readonly PopulationCode GeneticNote = Local("genetic-note", "Merknad om arvelige forhold");

    public static readonly PopulationCode MedicationFrequency = Local("medication-frequency", "Legemiddelfrekvens");
    public static readonly PopulationCode DrugAllergy = Local("drug-allergy", "Legemiddelallergi");
    public static readonly PopulationCode FolateBefore = Local("folate-before-pregnancy", "Folat før svangerskap");
    public static readonly PopulationCode FolateDuring = Local("folate-during-pregnancy", "Folat under svangerskap");

    public static readonly PopulationCode AboType = Local("abo-blood-type", "ABO-blodtype");
    public static readonly PopulationCode RhesusDType = Local("maternal-rhesus-d", "Mors RhD-status");
    public static readonly PopulationCode Hemoglobin = Local("hemoglobin-first-trimester", "B-Hemoglobin første trimester");
    public static readonly PopulationCode HemoglobinThirdTrimester = Local("hemoglobin-third-trimester", "B-Hemoglobin tredje trimester");
    public static readonly PopulationCode Ferritin = NlkCode("NPU19763", "P-Ferritin");
    public static readonly PopulationCode Hbv = Local("hbv-s-antigen-positive", "HBV s-antigen positiv");
    public static readonly PopulationCode HbvCore = Local("hbv-core-antibody-positive", "HBV core-antistoff positiv");
    public static readonly PopulationCode Hiv = NlkCode("NPU19649", "HIV 1+2 antistoff og antigen");
    public static readonly PopulationCode Syphilis = NlkCode("NPU03611", "Treponema pallidum-antistoff");
    public static readonly PopulationCode BloodAntibodies = Local("blood-antibodies", "Blodtypeantistoffer");
    public static readonly PopulationCode Chlamydia = NlkCode("NPU12331", "Chlamydia trachomatis DNA");
    public static readonly PopulationCode Toxoplasmosis = Local("toxoplasmosis-positive", "Toxoplasmoseprøve positiv");
    public static readonly PopulationCode Rubella = NlkCode("NPU12412", "Rubellavirus IgG");
    public static readonly PopulationCode HepatitisC = NlkCode("NPU12033", "Hepatitt C-antistoff");
    public static readonly PopulationCode MrsaVreEsbl = Local("mrsa-vre-esbl", "MRSA/VRE/ESBL");
    public static readonly PopulationCode HbA1c = NlkCode("NPU27300", "B-HbA1c");
    public static readonly PopulationCode GlucoseFasting = Local("glucose-tolerance-fasting", "Fastende glukose");
    public static readonly PopulationCode Glucose2Hour = Local("glucose-tolerance-2h", "Glukose etter 2 timer");
    public static readonly PopulationCode Gonorrhea = Local("gonorrhea", "Gonoré");
    public static readonly PopulationCode Cytomegalovirus = Local("cytomegalovirus", "Cytomegalovirus");
    public static readonly PopulationCode AsymptomaticBacteriuria = Local("asymptomatic-bacteriuria", "Asymptomatisk bakteriuri");
    public static readonly PopulationCode GroupBStreptococci = NlkCode("NPU18725", "Gruppe B-streptokokker");

    public static readonly PopulationCode RhesusConsent = Local("rhd-consent-fetal-typing", "Samtykke til foster-RhD-typing");
    public static readonly PopulationCode FetusRhesusWeek24 = Local("fetus-rhd-week-24", "Foster-RhD uke 24");
    public static readonly PopulationCode FetusRhesusResultDate = Local("fetus-rhd-result-date", "Dato for foster-RhD-resultat");
    public static readonly PopulationCode RhesusProphylaxisWeek28 = Local("rhd-prophylaxis-week-28", "RhD-profylakse uke 28");

    public static readonly PopulationCode Height = Local("pre-pregnancy-height", "Høyde før svangerskap");
    public static readonly PopulationCode PrePregnancyWeight = Local("pre-pregnancy-weight", "Vekt før svangerskap");
    public static readonly PopulationCode PrePregnancyBmi = Local("pre-pregnancy-bmi", "BMI før svangerskap");
    public static readonly PopulationCode SymphysisFundalHeight = Local("symphysis-fundal-height", "Symfyse-fundusmål");
    public static readonly PopulationCode MotherWeight = Local("mother-weight", "Mors vekt");
    public static readonly PopulationCode BloodPressure = Local("blood-pressure", "Blodtrykk");
    public static readonly PopulationCode Systolic = Local("systolic-blood-pressure", "Systolisk blodtrykk");
    public static readonly PopulationCode Diastolic = Local("diastolic-blood-pressure", "Diastolisk blodtrykk");
    public static readonly PopulationCode UrineProtein = new(Nlk, "NPU04206", "Protein i urin");
    public static readonly PopulationCode Edema = Local("edema", "Ødem");
    public static readonly PopulationCode FetalHeartRate = Local("fetal-heart-rate", "Fosterlyd per minutt");
    public static readonly PopulationCode FetalPresentationLie = Local("fetal-presentation-lie", "Fosterpresentasjon/leie");
    public static readonly PopulationCode MotherFeelsMovements = Local("mother-feels-baby-movements", "Mor kjenner liv");

    public static PopulationCode MedicalCondition(string code, string display) => Local($"medical-condition-{code}", display);
    public static PopulationCode Lifestyle(string code, string display) => new(Volven8536, code, display);
    public static PopulationCode Local(string code, string display) => new(System, code, display);
    private static PopulationCode NlkCode(string code, string display) => new(Nlk, code, display);
}
