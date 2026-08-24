using System.Globalization;
using System.Text.RegularExpressions;
using PopulationDataFacade.Core;

namespace PopulationDataFacade.Infrastructure.Dhg;

public sealed partial class DhgPopulationSnapshotFactory
{
    public PopulationSnapshot Create(
        string logicalPatientId,
        DhgStatusResponse status,
        DhgMaternityRecord record)
    {
        var observations = new List<PopulationObservation>();
        var encounters = new List<PopulationEncounter>();
        var careTeams = new List<PopulationCareTeam>();
        var fetuses = new Dictionary<int, PopulationFetusPatient>();

        var mother = Active(record.Mother);
        var language = ToCodedValue(mother?.Language);
        if (language?.System != PopulationCodes.Volven3303) language = null;
        var patient = new PopulationPatient(
            logicalPatientId,
            language,
            mother?.NeedsLanguageInterpreter,
            mother?.Metadata?.LastUpdated ?? record.Metadata?.RecordLastUpdated);

        MapMotherFindings(mother, observations);
        MapCurrentPregnancy(Active(record.CurrentPregnancy), observations);
        MapPreviousPregnancies(Active(record.PreviousPregnancies), observations);
        MapGeneticDisorders(Active(record.GeneticDisorders), observations);
        MapMedicalConditions(Active(record.MedicalConditions), observations);
        MapMedication(Active(record.Medication), observations);
        MapLifestyle(Active(record.LifestyleFactors), observations);
        MapClinicalTests(Active(record.ClinicalTests), observations);
        MapRhesus(Active(record.RhesusDNegative), observations);
        MapVitalMeasurementsBeforePregnancy(
            Active(record.VitalMeasurementsBeforePregnancy),
            observations);
        MapSymphysisFundalHeights(record.SymphysisFundalHeights, observations);
        MapAntenatalAppointments(
            logicalPatientId,
            record.AntenatalAppointments,
            observations,
            encounters,
            fetuses);
        MapPointsOfContact(Active(record.PointsOfContact), careTeams);

        return new PopulationSnapshot(
            patient,
            observations,
            encounters,
            status.LastChangedDateTime ?? record.Metadata?.RecordLastUpdated,
            status.HasActiveMaternityRecord == true,
            careTeams,
            fetuses.Values.OrderBy(fetus => fetus.LogicalId, StringComparer.Ordinal).ToArray());
    }

    private static void MapCurrentPregnancy(DhgCurrentPregnancy? source, List<PopulationObservation> output)
    {
        if (source is null) return;
        var updated = source.Metadata?.LastUpdated;
        AddDate(output, Id(source.Metadata, "date-last-period"), PopulationCodes.DateLastPeriod, source.DateLastPeriod, updated);
        AddDate(output, Id(source.Metadata, "due-date-last-period"), PopulationCodes.DueDateLastPeriod, source.DueDate, updated);
        AddDate(output, Id(source.Metadata, "due-date-ultrasound"), PopulationCodes.DueDateUltrasound, source.DueDateBasedOnUltrasound, updated);
        AddDate(output, Id(source.Metadata, "due-date-corrected"), PopulationCodes.CorrectedDueDate, source.DueDateCorrectedDate, updated);
        AddInteger(
            output,
            Id(source.Metadata, "number-of-fetuses"),
            PopulationCodes.NumberOfFetuses,
            source.NumberOfFetuses is > 0 ? source.NumberOfFetuses : null,
            updated);
        var assistedConceptionDate = source.AssistedConception?.HadAssistedConception == true &&
                                     source.AssistedConception.DateAssistedConception is not null
            ? new EffectiveDate(source.AssistedConception.DateAssistedConception.Value)
            : null;
        AddBoolean(
            output,
            Id(source.Metadata, "assisted-conception"),
            PopulationCodes.AssistedConception,
            source.AssistedConception?.HadAssistedConception,
            updated,
            effective: assistedConceptionDate);
        AddBoolean(
            output,
            Id(source.Metadata, "prenatal-diagnostics-information-provided"),
            PopulationCodes.PrenatalDiagnosticsInformationProvided,
            source.HasPrenatalDiagnosticsTests,
            updated);
        AddBoolean(output, Id(source.Metadata, "birth-preparation-talk"), PopulationCodes.BirthPreparationTalk, source.BirthPreparationTalk, updated);
        AddBoolean(output, Id(source.Metadata, "breastfeeding-guidance"), PopulationCodes.BreastfeedingGuidance, source.BreastfeedingGuidance, updated);
    }

    private static void MapMotherFindings(DhgMother? source, List<PopulationObservation> output)
    {
        if (source is null) return;
        var updated = source.Metadata?.LastUpdated;
        AddBoolean(
            output,
            Id(source.Metadata, "cohabiting-coparent"),
            PopulationCodes.CohabitingCoparent,
            source.CohabitingCoparent,
            updated,
            category: "social-history",
            note: "Source-svaret beholdes uten å utlede relasjon, foreldreansvar eller husstandsmedlemmer.");
        AddText(
            output,
            Id(source.Metadata, "cohabiting-coparent-note"),
            PopulationCodes.CohabitingCoparentNote,
            source.CohabitingCoparentNote,
            updated,
            note: "Source text beholdes ordrett og tolkes ikke til relasjon, adresse eller sosialfaglig vurdering.",
            category: "social-history");
    }

    private static void MapPreviousPregnancies(DhgPreviousPregnancies? source, List<PopulationObservation> output)
    {
        if (source is null) return;
        var updated = source.Metadata?.LastUpdated;
        AddInteger(output, Id(source.Metadata, "previous-pregnancies"), PopulationCodes.PreviousPregnancies, source.NumberOfPreviousPregnancies, updated);
        AddInteger(output, Id(source.Metadata, "previous-live-births"), PopulationCodes.PreviousLiveBirths, source.NumberOfPreviousLiveBirths, updated);
        AddInteger(output, Id(source.Metadata, "spontaneous-miscarriages"), PopulationCodes.SpontaneousMiscarriages, source.SpontaneousMiscarriages, updated);
        AddInteger(output, Id(source.Metadata, "stillbirths-22-weeks"), PopulationCodes.StillBirths22Weeks, source.StillBirths22Weeks, updated);
        AddInteger(output, Id(source.Metadata, "ectopic-pregnancies"), PopulationCodes.EctopicPregnancies, source.NumberOfEctopicPregnancies, updated);
        AddText(
            output,
            Id(source.Metadata, "previous-pregnancies-note"),
            PopulationCodes.PreviousPregnanciesNote,
            source.Note,
            updated,
            note: "Source text beholdes ordrett og tolkes ikke som svangerskapsutfall, diagnose, prosedyre eller beregningsgrunnlag.");
    }

    private static void MapGeneticDisorders(DhgGeneticDisorders? source, List<PopulationObservation> output)
    {
        if (source is null) return;
        var updated = source.Metadata?.LastUpdated;
        AddBoolean(output, Id(source.Metadata, "genetic-none-known"), PopulationCodes.NoKnownGeneticDisorders, source.NoneKnown, updated);
        AddBoolean(output, Id(source.Metadata, "parents-are-relatives"), PopulationCodes.ParentsAreRelatives, source.ParentsAreRelatives, updated);
        AddBoolean(output, Id(source.Metadata, "genetic-other"), PopulationCodes.OtherGeneticDisorder, source.Other, updated);
        AddText(output, Id(source.Metadata, "genetic-note"), PopulationCodes.GeneticDisordersNote, source.Note, updated);
        AddBoolean(
            output,
            Id(source.Metadata, "genetic-hip-dysplasia-family-history"),
            PopulationCodes.HipDysplasiaFamilyHistory,
            source.HipDysplasia,
            updated,
            note: "Source-feltet beholdes som et familiehistorisk svar; berørt person og klinisk diagnose utledes ikke.");
    }

    private static void MapMedicalConditions(DhgMedicalConditions? source, List<PopulationObservation> output)
    {
        if (source is null) return;
        var updated = source.Metadata?.LastUpdated;
        AddBoolean(
            output,
            Id(source.Metadata, "medical-nothing-particular"),
            PopulationCodes.NothingParticularMedical,
            source.NothingParticular,
            updated,
            note: "Uttrykker bare om «Ingenting spesielt» er markert i DHG. false betyr ikke at en sykdom er identifisert.");
        var fields = new (string Suffix, PopulationCode Code, bool? Value)[]
        {
            ("heart-disease", PopulationCodes.HeartDisease, source.HeartDisease),
            ("high-blood-pressure", PopulationCodes.HypertensiveDisorder, source.HighBloodPressure),
            ("diabetes", PopulationCodes.DiabetesMellitus, source.Diabetes),
            ("epilepsy", PopulationCodes.Epilepsy, source.Epilepsy),
            ("thrombosis", PopulationCodes.Thrombosis, source.Thrombosis),
            ("autoimmune-disease", PopulationCodes.AutoimmuneDisease, source.AutoimmuneDisease),
            ("mental-health", PopulationCodes.MentalDisorder, source.MentalHealth)
        };

        foreach (var field in fields)
        {
            AddBoolean(output, Id(source.Metadata, $"medical-{field.Suffix}"), field.Code, field.Value, updated);
        }

        AddBoolean(
            output,
            Id(source.Metadata, "medical-kidney-or-urinary-tract-disease"),
            PopulationCodes.KidneyOrUrinaryTractDisease,
            source.KidneyUrinaryTractDiseases,
            updated,
            note: "Sammensatt DHG-felt. Angir ikke om funnet gjelder nyresykdom, urinveissykdom eller begge.");
        AddBoolean(
            output,
            Id(source.Metadata, "medical-allergy-or-asthma"),
            PopulationCodes.AllergyOrAsthma,
            source.AllergiesAsthma,
            updated,
            note: "Sammensatt DHG-felt. Angir ikke om funnet gjelder allergi, astma eller begge.");
        AddBoolean(
            output,
            Id(source.Metadata, "medical-gynecological-condition-or-intervention"),
            PopulationCodes.GynecologicalConditionOrIntervention,
            source.GynecologicalConditions,
            updated,
            note: "Sammensatt DHG-felt. Angir ikke om funnet gjelder sykdom, inngrep, operasjon eller en kombinasjon.");
        AddBoolean(
            output,
            Id(source.Metadata, "medical-other"),
            PopulationCodes.OtherMedicalCondition,
            source.Other,
            updated,
            note: "Angir bare om annen medisinsk tilstand er markert i DHG. Diagnose utledes ikke.");
        AddText(
            output,
            Id(source.Metadata, "medical-note"),
            PopulationCodes.MedicalConditionsNote,
            source.Note,
            updated,
            note: "Source text beholdes ordrett og tolkes ikke som diagnose, legemiddel, prosedyre eller berørt person.");
    }

    private static void MapMedication(DhgMedication? source, List<PopulationObservation> output)
    {
        if (source is null) return;
        var updated = source.Metadata?.LastUpdated;
        AddBoolean(output, Id(source.Metadata, "drug-allergy"), PopulationCodes.DrugAllergy, source.DrugAllergy, updated);
        AddBoolean(output, Id(source.Metadata, "folate-before"), PopulationCodes.FolateIntake, source.Folate?.TakenBefore, updated, note: "Før svangerskapet");
        AddBoolean(output, Id(source.Metadata, "folate-during"), PopulationCodes.FolateIntake, source.Folate?.TakenDuring, updated, note: "Under svangerskapet");
        AddText(
            output,
            Id(source.Metadata, "medication-frequency"),
            PopulationCodes.MedicationFrequency,
            source.MedicationFrequency,
            updated,
            note: "Uparset DHG-verdi; det utledes ikke legemiddel, dose eller standardisert frekvens.");
        AddText(
            output,
            Id(source.Metadata, "medication-note"),
            PopulationCodes.MedicationNote,
            source.Note,
            updated,
            note: "Source text beholdes ordrett og tolkes ikke som legemiddel, dose, indikasjon eller instruksjon.");
    }

    private static void MapLifestyle(DhgLifestyleFactors? source, List<PopulationObservation> output)
    {
        if (source?.Stimuli is null) return;
        var updated = source.Metadata?.LastUpdated;
        var index = 0;
        foreach (var item in source.Stimuli.OfType<DhgStimulus>())
        {
            index++;
            var type = ToCodedValue(item.StimuliType);
            if (type is null || type.System != PopulationCodes.Volven8536) continue;

            var code = PopulationCodes.Lifestyle(type.Code, type.Display ?? type.Code);
            AddStimulusFrequency(output, source.Metadata, code, index, "first-consultation", "Ved første konsultasjon", item.FirstConsultation, updated, source.Note);
            AddStimulusFrequency(output, source.Metadata, code, index, "week-36", "Ved uke 36", item.AtWeek36, updated, source.Note);
        }
    }

    private static void AddStimulusFrequency(
        List<PopulationObservation> output,
        DhgResourceMetadata? metadata,
        PopulationCode code,
        int index,
        string suffix,
        string context,
        DhgStimuliFrequency? source,
        DateTimeOffset? updated,
        string? sourceNote)
    {
        var frequency = ToCodedValue(source?.Frequency);
        if (frequency is null || frequency.System != PopulationCodes.Volven8537) return;

        var note = string.IsNullOrWhiteSpace(sourceNote)
            ? context
            : $"{context}. {sourceNote}";
        var dailyCount = source?.DailyCount;
        IReadOnlyList<PopulationComponent>? components = dailyCount is >= 0
            ? [new PopulationComponent(PopulationCodes.DailyStimulusCount, new IntegerValue(dailyCount.Value))]
            : null;
        output.Add(Observation(
            Id(metadata, $"lifestyle-{code.Code}-{suffix}-{index}"),
            code,
            frequency,
            "social-history",
            updated,
            components: components,
            note: note));
    }

    private static void MapClinicalTests(DhgClinicalTests? source, List<PopulationObservation> output)
    {
        if (source is null) return;
        var updated = source.Metadata?.LastUpdated;
        AddQuantity(output, Id(source.Metadata, "hemoglobin"), PopulationCodes.Hemoglobin, source.Hemoglobin, "g/dL", "g/dL", updated);
        AddQuantity(output, Id(source.Metadata, "hemoglobin-3trimester"), PopulationCodes.Hemoglobin, source.HemoglobinAtThirdTrimester, "g/dL", "g/dL", updated, note: "Tredje trimester");
        AddQuantity(output, Id(source.Metadata, "ferritin"), PopulationCodes.Ferritin, source.Ferritin, "µg/L", "ug/L", updated);
        AddBooleanLab(output, Id(source.Metadata, "hbv"), PopulationCodes.Hbv, source.Hbv, updated);
        AddBooleanLab(output, Id(source.Metadata, "hbv-core"), PopulationCodes.HbvCoreAntibodyTestResult, source.HbvCore, updated);
        AddBooleanLab(output, Id(source.Metadata, "hiv"), PopulationCodes.HivTestResult, source.Hiv, updated);
        AddBooleanLab(output, Id(source.Metadata, "syphilis"), PopulationCodes.SyphilisTestResult, source.Syphilis, updated);
        AddBooleanLab(output, Id(source.Metadata, "blood-antibodies"), PopulationCodes.BloodTypeAntibodyTestResult, source.BloodAntibodies, updated);
        AddBooleanLab(output, Id(source.Metadata, "chlamydia"), PopulationCodes.ChlamydiaTestResult, source.Chlamydia, updated);
        AddBooleanLab(output, Id(source.Metadata, "toxoplasmosis"), PopulationCodes.ToxoplasmosisTestResult, source.Toxoplasmosis, updated);
        AddBooleanLab(output, Id(source.Metadata, "rubella-igg"), PopulationCodes.RubellaIgg, source.RubellaAntigen, updated);
        AddBooleanLab(output, Id(source.Metadata, "hepatitis-c"), PopulationCodes.HepatitisCTestResult, source.HepatitisC, updated);
        AddBooleanLab(output, Id(source.Metadata, "mrsa-vre-esbl"), PopulationCodes.MrsaVreEsblTestResult, source.MrsaVreEsbl, updated);
        AddBooleanLab(output, Id(source.Metadata, "gonorrhea"), PopulationCodes.GonorrheaTestResult, source.Gonorrhea, updated);
        AddBooleanLab(output, Id(source.Metadata, "cytomegalovirus"), PopulationCodes.CytomegalovirusTestResult, source.CytomegaloVirus, updated);
        AddBooleanLab(output, Id(source.Metadata, "asymptomatic-bacteriuria"), PopulationCodes.AsymptomaticBacteriuriaTestResult, source.AsymptomaticBacteriuria, updated);
        AddBooleanLab(output, Id(source.Metadata, "group-b-streptococci"), PopulationCodes.GroupBStreptococciTestResult, source.GroupBStreptococci, updated);
        AddText(
            output,
            Id(source.Metadata, "clinical-tests-note"),
            PopulationCodes.ClinicalTestsNote,
            source.Note,
            updated,
            note: "Source text beholdes ordrett og tolkes ikke som analytt, resultat, diagnose eller vurdering.");
        AddCoded(output, Id(source.Metadata, "abo-type"), PopulationCodes.AboType, ToAboValue(source.AboRh?.AboType), updated);
        AddCoded(output, Id(source.Metadata, "rhesus-d-type"), PopulationCodes.RhesusDType, ToRhesusDValue(source.AboRh?.RhesusDType), updated);
        AddQuantity(output, Id(source.Metadata, "hba1c"), PopulationCodes.HbA1c, source.BHbA1c, "mmol/mol", "mmol/mol", updated);
        AddQuantity(output, Id(source.Metadata, "glucose-fasting"), PopulationCodes.GlucoseFasting, source.GlucoseTolerance?.FastingGlucoseLevel, "mmol/L", "mmol/L", updated, source.GlucoseTolerance?.TestDate);
        AddQuantity(output, Id(source.Metadata, "glucose-2h"), PopulationCodes.Glucose2Hour, source.GlucoseTolerance?.PostTwoHourGlucoseLevel, "mmol/L", "mmol/L", updated, source.GlucoseTolerance?.TestDate);
    }

    private static void MapRhesus(DhgRhesusDNegative? source, List<PopulationObservation> output)
    {
        if (source is null) return;
        var updated = source.Metadata?.LastUpdated;
        AddBoolean(output, Id(source.Metadata, "rhd-prophylaxis-week28"), PopulationCodes.RhesusProphylaxis, source.ProphylaxisAtWeek28, updated, "therapy");
    }

    private static void MapVitalMeasurementsBeforePregnancy(
        DhgVitalMeasurementsBeforePregnancy? source,
        List<PopulationObservation> output)
    {
        if (source is null) return;

        const string context = "Før svangerskapet; measurement time er ikke oppgitt av DHG";
        var updated = source.Metadata?.LastUpdated;
        AddQuantity(
            output,
            Id(source.Metadata, "pre-pregnancy-height"),
            PopulationCodes.BodyHeight,
            source.Height,
            "cm",
            "cm",
            updated,
            category: "vital-signs",
            note: context);
        AddQuantity(
            output,
            Id(source.Metadata, "pre-pregnancy-weight"),
            PopulationCodes.MotherWeight,
            source.PrePregnancyWeight,
            "kg",
            "kg",
            updated,
            category: "vital-signs",
            note: context);
        AddQuantity(
            output,
            Id(source.Metadata, "pre-pregnancy-bmi"),
            PopulationCodes.BodyMassIndex,
            source.BMI,
            "kg/m²",
            "kg/m2",
            updated,
            category: "vital-signs",
            note: context);
    }

    private static void MapSymphysisFundalHeights(IEnumerable<DhgSymphysisFundalHeight>? sources, List<PopulationObservation> output)
    {
        if (sources is null) return;
        var index = 0;
        foreach (var source in sources.OfType<DhgSymphysisFundalHeight>().Where(x => x.Metadata?.EnteredInError != true))
        {
            index++;
            if (source.Measurement is null or <= 0) continue;
            output.Add(Observation(
                Id(source.Metadata, $"sfh-{index}"),
                PopulationCodes.SymphysisFundalHeight,
                new QuantityValue(source.Measurement.Value, "cm", PopulationCodes.Ucum, "cm"),
                "vital-signs",
                source.Metadata?.LastUpdated,
                source.MeasurementDate is null ? null : new EffectiveDate(source.MeasurementDate.Value)));
        }
    }

    private static void MapAntenatalAppointments(
        string maternalPatientId,
        IEnumerable<DhgAntenatalAppointment>? sources,
        List<PopulationObservation> output,
        List<PopulationEncounter> encounters,
        Dictionary<int, PopulationFetusPatient> fetuses)
    {
        if (sources is null) return;
        var appointments = sources
            .OfType<DhgAntenatalAppointment>()
            .Where(x => x.Metadata?.EnteredInError != true)
            .OrderBy(x => x.AppointmentDate)
            .ToList();
        var index = 0;
        foreach (var source in appointments)
        {
            index++;
            if (source.AppointmentDate is null) continue;
            var encounterId = Id(source.Metadata, $"antenatal-{index}");
            var effective = new EffectiveDate(source.AppointmentDate.Value);
            encounters.Add(new PopulationEncounter(encounterId, source.AppointmentDate.Value, source.Metadata?.LastUpdated));

            if (source.PregnancyWeek is > 0 &&
                source.DaysAfterFullPregnancyWeek is null or (>= 0 and <= 6))
            {
                var daysAfterFullWeek = source.DaysAfterFullPregnancyWeek ?? 0;
                var totalDays = checked((source.PregnancyWeek.Value * 7) + daysAfterFullWeek);
                output.Add(Observation(
                    Id(source.Metadata, $"gestational-age-{index}"),
                    PopulationCodes.GestationalAge,
                    new QuantityValue(totalDays, "days", PopulationCodes.Ucum, "d"),
                    "survey",
                    source.Metadata?.LastUpdated,
                    effective,
                    encounterId: encounterId,
                    note: $"{source.PregnancyWeek.Value}+{daysAfterFullWeek}"));
            }

            AddQuantity(output, Id(source.Metadata, $"mother-weight-{index}"), PopulationCodes.MotherWeight, source.MotherWeight, "kg", "kg", source.Metadata?.LastUpdated, source.AppointmentDate, encounterId, "vital-signs");
            AddInteger(
                output,
                Id(source.Metadata, $"edema-grade-{index}"),
                PopulationCodes.EdemaGrade,
                source.Edema is >= 0 and <= 3 ? source.Edema : null,
                source.Metadata?.LastUpdated,
                category: "exam",
                effective: effective,
                encounterId: encounterId,
                note: "Rå DHG-grad beholdes; betydningen av skalaens enkelte trinn utledes ikke.");
            AddBoolean(
                output,
                Id(source.Metadata, $"antenatal-medication-{index}"),
                PopulationCodes.AntenatalMedicationReported,
                source.Medication,
                source.Metadata?.LastUpdated,
                effective: effective,
                encounterId: encounterId,
                note: "Source-svaret beholdes uten å utlede legemiddel, dose, indikasjon eller behandlingsstatus.");
            var appointmentNote = CleanText(source.Note);
            if (appointmentNote is not null)
            {
                output.Add(Observation(
                    Id(source.Metadata, $"antenatal-note-{index}"),
                    PopulationCodes.AntenatalAppointmentNote,
                    new TextValue(appointmentNote),
                    "survey",
                    source.Metadata?.LastUpdated,
                    effective,
                    encounterId: encounterId,
                    note: "Source text beholdes ordrett og tolkes ikke som diagnose, legemiddel, prosedyre, måling eller vurdering."));
            }
            MapBloodPressure(source, output, effective, encounterId, index);
            var urineProteinResult = ToUrineProteinResult(source.ProteinInUrineTestResult);
            if (urineProteinResult is not null)
            {
                output.Add(Observation(Id(source.Metadata, $"urine-protein-{index}"), PopulationCodes.UrineProtein, urineProteinResult, "laboratory", source.Metadata?.LastUpdated, effective, encounterId: encounterId));
            }

            MapFetalFindings(
                maternalPatientId,
                source,
                output,
                fetuses,
                effective,
                encounterId,
                index);
        }
    }

    private static void MapFetalFindings(
        string maternalPatientId,
        DhgAntenatalAppointment appointment,
        List<PopulationObservation> output,
        Dictionary<int, PopulationFetusPatient> fetuses,
        EffectiveDate effective,
        string encounterId,
        int appointmentIndex)
    {
        if (appointment.FetusesVitalSigns is null) return;

        foreach (var fetus in appointment.FetusesVitalSigns
                     .OfType<DhgFetusVitalSigns>()
                     .Where(candidate => candidate.FetusId is > 0))
        {
            var sourceFetusId = fetus.FetusId!.Value;
            if (!fetuses.TryGetValue(sourceFetusId, out var fetusPatient))
            {
                fetusPatient = new PopulationFetusPatient(
                    FetalPatientId.Create(maternalPatientId, sourceFetusId),
                    appointment.Metadata?.LastUpdated);
                fetuses.Add(sourceFetusId, fetusPatient);
            }
            else if (appointment.Metadata?.LastUpdated is { } candidateLastUpdated &&
                     (fetusPatient.LastUpdated is null || candidateLastUpdated > fetusPatient.LastUpdated))
            {
                fetusPatient = fetusPatient with { LastUpdated = candidateLastUpdated };
                fetuses[sourceFetusId] = fetusPatient;
            }

            if (fetus.FetalHeartRate is > 0)
            {
                output.Add(Observation(
                    Id(appointment.Metadata, $"fetal-heart-rate-{sourceFetusId}-{appointmentIndex}"),
                    PopulationCodes.FetalHeartRate,
                    new QuantityValue(
                        fetus.FetalHeartRate.Value,
                        "slag/min",
                        PopulationCodes.Ucum,
                        "{beats}/min"),
                    "vital-signs",
                    appointment.Metadata?.LastUpdated,
                    effective,
                    encounterId: encounterId,
                    focusPatientId: fetusPatient.LogicalId));
            }

            var presentation = ToCodedValue(fetus.FetalPresentationLie);
            if (presentation?.System == PopulationCodes.Volven8534)
            {
                output.Add(Observation(
                    Id(appointment.Metadata, $"fetal-presentation-{sourceFetusId}-{appointmentIndex}"),
                    PopulationCodes.FetalPresentationLie,
                    presentation,
                    "exam",
                    appointment.Metadata?.LastUpdated,
                    effective,
                    encounterId: encounterId,
                    focusPatientId: fetusPatient.LogicalId));
            }

            if (fetus.MotherFeelsBabyMovements is not null)
            {
                output.Add(Observation(
                    Id(appointment.Metadata, $"fetal-movements-{sourceFetusId}-{appointmentIndex}"),
                    PopulationCodes.FetalMovementsReported,
                    new BooleanValue(fetus.MotherFeelsBabyMovements.Value),
                    "survey",
                    appointment.Metadata?.LastUpdated,
                    effective,
                    encounterId: encounterId,
                    focusPatientId: fetusPatient.LogicalId));
            }

            var note = CleanText(fetus.Note);
            if (note is not null)
            {
                output.Add(Observation(
                    Id(appointment.Metadata, $"fetal-note-{sourceFetusId}-{appointmentIndex}"),
                    PopulationCodes.FetalFindingsNote,
                    new TextValue(note),
                    "exam",
                    appointment.Metadata?.LastUpdated,
                    effective,
                    encounterId: encounterId,
                    focusPatientId: fetusPatient.LogicalId));
            }
        }
    }

    private static void MapPointsOfContact(
        DhgPointsOfContact? source,
        List<PopulationCareTeam> output)
    {
        if (source is null) return;

        var generalPractitionerName = CleanText(source.GeneralPractitioner?.Name);
        var generalPractitionerOrganizationName = CleanText(source.GeneralPractitioner?.OrganizationName);
        var generalPractitionerHprNumber = CleanText(source.GeneralPractitioner?.HprNumber);
        var generalPractitionerOrganizationId = CleanText(source.GeneralPractitioner?.OrganizationId);
        var midwifeName = CleanText(source.Midwife?.Name);
        var midwifeOrganizationName = CleanText(source.Midwife?.OrganizationName);
        var midwifeHprNumber = CleanText(source.Midwife?.HprNumber);
        var maternityHealthcareCentre = CleanText(source.MaternityHealthcareCentre);
        if (generalPractitionerName is null &&
            generalPractitionerOrganizationName is null &&
            generalPractitionerHprNumber is null &&
            generalPractitionerOrganizationId is null &&
            midwifeName is null &&
            midwifeOrganizationName is null &&
            midwifeHprNumber is null &&
            maternityHealthcareCentre is null)
            return;

        var generalPractitioner = generalPractitionerName is null &&
                                  generalPractitionerOrganizationName is null &&
                                  generalPractitionerHprNumber is null &&
                                  generalPractitionerOrganizationId is null
            ? null
            : new PopulationCareTeamMember(
                generalPractitionerName,
                generalPractitionerOrganizationName,
                generalPractitionerHprNumber,
                generalPractitionerOrganizationId);
        var midwife = midwifeName is null &&
                      midwifeOrganizationName is null &&
                      midwifeHprNumber is null
            ? null
            : new PopulationCareTeamMember(
                midwifeName,
                midwifeOrganizationName,
                midwifeHprNumber);
        output.Add(new PopulationCareTeam(
            Id(source.Metadata, "pregnancy-care-team"),
            midwife,
            maternityHealthcareCentre,
            source.Metadata?.LastUpdated,
            generalPractitioner));
    }

    private static void MapBloodPressure(DhgAntenatalAppointment source, List<PopulationObservation> output, EffectiveDate effective, string encounterId, int index)
    {
        if (string.IsNullOrWhiteSpace(source.BloodPressure)) return;
        var match = BloodPressurePattern().Match(source.BloodPressure);
        if (!match.Success ||
            !int.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out var systolic) ||
            !int.TryParse(match.Groups[2].Value, CultureInfo.InvariantCulture, out var diastolic) ||
            systolic <= 0 ||
            diastolic <= 0) return;

        output.Add(Observation(
            Id(source.Metadata, $"blood-pressure-{index}"),
            PopulationCodes.BloodPressure,
            null,
            "vital-signs",
            source.Metadata?.LastUpdated,
            effective,
            [
                new PopulationComponent(PopulationCodes.Systolic, new QuantityValue(systolic, "mmHg", PopulationCodes.Ucum, "mm[Hg]")),
                new PopulationComponent(PopulationCodes.Diastolic, new QuantityValue(diastolic, "mmHg", PopulationCodes.Ucum, "mm[Hg]"))
            ],
            encounterId));
    }

    private static void AddBooleanLab(List<PopulationObservation> output, string id, PopulationCode code, bool? value, DateTimeOffset? updated) =>
        AddCoded(output, id, code, ToPositiveNegativeResult(value), updated);

    private static void AddBoolean(List<PopulationObservation> output, string id, PopulationCode code, bool? value, DateTimeOffset? updated, string category = "survey", PopulationEffective? effective = null, string? encounterId = null, string? note = null)
    {
        if (value is not null) output.Add(Observation(id, code, new BooleanValue(value.Value), category, updated, effective, encounterId: encounterId, note: note));
    }

    private static void AddInteger(List<PopulationObservation> output, string id, PopulationCode code, int? value, DateTimeOffset? updated, string category = "survey", PopulationEffective? effective = null, string? encounterId = null, string? note = null)
    {
        if (value is not null) output.Add(Observation(id, code, new IntegerValue(value.Value), category, updated, effective, encounterId: encounterId, note: note));
    }

    private static void AddDate(List<PopulationObservation> output, string id, PopulationCode code, DateOnly? value, DateTimeOffset? updated)
    {
        if (value is not null) output.Add(Observation(id, code, new DateValue(value.Value), "survey", updated));
    }

    private static void AddText(List<PopulationObservation> output, string id, PopulationCode code, string? value, DateTimeOffset? updated, string? note = null, string category = "survey")
    {
        var text = CleanText(value);
        if (text is not null) output.Add(Observation(id, code, new TextValue(text), category, updated, note: note));
    }

    private static void AddCoded(List<PopulationObservation> output, string id, PopulationCode code, CodedValue? value, DateTimeOffset? updated)
    {
        if (value is not null) output.Add(Observation(id, code, value, "laboratory", updated));
    }

    private static void AddQuantity<T>(List<PopulationObservation> output, string id, PopulationCode code, T? value, string unit, string unitCode, DateTimeOffset? updated, DateOnly? effectiveDate = null, string? encounterId = null, string category = "laboratory", string? note = null)
        where T : struct, IConvertible
    {
        if (value is null) return;
        var numeric = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        if (numeric <= 0) return;
        output.Add(Observation(id, code, new QuantityValue(numeric, unit, PopulationCodes.Ucum, unitCode), category, updated, effectiveDate is null ? null : new EffectiveDate(effectiveDate.Value), encounterId: encounterId, note: note));
    }

    private static PopulationObservation Observation(string id, PopulationCode code, PopulationValue? value, string category, DateTimeOffset? updated, PopulationEffective? effective = null, IReadOnlyList<PopulationComponent>? components = null, string? encounterId = null, string? note = null, string? focusPatientId = null) =>
        new(id, code, value, category, updated, effective, components, encounterId, note, focusPatientId);

    private static T? Active<T>(T? source) where T : class
    {
        if (source is null) return null;
        var metadata = source.GetType().GetProperty("Metadata")?.GetValue(source) as DhgResourceMetadata;
        return metadata?.EnteredInError == true ? null : source;
    }

    private static CodedValue? ToCodedValue(DhgCodeAndSystem? source)
    {
        if (source?.Code is null) return null;
        var system = NormalizeCodeSystem(source.CodeSystem);
        return system is null ? null : new CodedValue(system, source.Code, source.Display);
    }

    private static string? CleanText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeCodeSystem(string? codeSystem)
    {
        if (string.IsNullOrWhiteSpace(codeSystem)) return null;
        return codeSystem switch
        {
            "VOLVEN_3303" => PopulationCodes.Volven3303,
            "VOLVEN_8340" => PopulationCodes.Volven8340,
            "VOLVEN_8534" => PopulationCodes.Volven8534,
            "VOLVEN_8536" => PopulationCodes.Volven8536,
            "VOLVEN_8537" => PopulationCodes.Volven8537,
            _ when OidPattern().IsMatch(codeSystem) => $"urn:oid:{codeSystem}",
            _ when Uri.TryCreate(codeSystem, UriKind.Absolute, out _) => codeSystem,
            _ => null
        };
    }

    private static CodedValue? ToAboValue(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "A" => new(PopulationCodes.SnomedCt, "112144000", "Blood group A"),
        "B" => new(PopulationCodes.SnomedCt, "112149005", "Blood group B"),
        "AB" => new(PopulationCodes.SnomedCt, "165743006", "Blood group AB"),
        "O" => new(PopulationCodes.SnomedCt, "58460004", "Blood group O"),
        _ => null
    };

    private static CodedValue? ToRhesusDValue(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "POSITIVE" or "POSTIVE" => new(PopulationCodes.SnomedCt, "165747007", "RhD positive"),
        "NEGATIVE" => new(PopulationCodes.SnomedCt, "165746003", "RhD negative"),
        _ => null
    };

    private static CodedValue? ToPositiveNegativeResult(bool? value) => value switch
    {
        true => new(PopulationCodes.Volven8340, "T002", "Positiv"),
        false => new(PopulationCodes.Volven8340, "T008", "Negativ"),
        null => null
    };

    private static CodedValue? ToUrineProteinResult(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "NEG" => new(PopulationCodes.Volven8340, "T008", "Negativ"),
        "SPOR" => new(PopulationCodes.Volven8340, "T052", "Spor"),
        "1+" => new(PopulationCodes.Volven8340, "T048", "1+"),
        "2+" => new(PopulationCodes.Volven8340, "T049", "2+"),
        "3+" => new(PopulationCodes.Volven8340, "T050", "3+"),
        _ => null
    };

    private static string Id(DhgResourceMetadata? metadata, string suffix)
    {
        var raw = $"{metadata?.Id ?? "dhg"}-{suffix}";
        var cleaned = InvalidFhirIdCharacters().Replace(raw, "-").Trim('-');
        return cleaned.Length <= 64 ? cleaned : cleaned[..64];
    }

    [GeneratedRegex("^\\s*(\\d{2,3})\\s*/\\s*(\\d{2,3})\\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex BloodPressurePattern();

    [GeneratedRegex("[^A-Za-z0-9\\-.]", RegexOptions.CultureInvariant)]
    private static partial Regex InvalidFhirIdCharacters();

    [GeneratedRegex("^\\d+(?:\\.\\d+)+$", RegexOptions.CultureInvariant)]
    private static partial Regex OidPattern();
}
