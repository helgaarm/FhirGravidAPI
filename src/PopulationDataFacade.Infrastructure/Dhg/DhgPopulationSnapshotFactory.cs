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

        var mother = Active(record.Mother);
        var patient = new PopulationPatient(
            logicalPatientId,
            ToCodedValue(mother?.Language),
            mother?.NeedsLanguageInterpreter,
            mother?.Metadata?.LastUpdated ?? record.Metadata?.RecordLastUpdated);

        MapCurrentPregnancy(Active(record.CurrentPregnancy), observations);
        MapPreviousPregnancies(Active(record.PreviousPregnancies), observations);
        MapGeneticDisorders(Active(record.GeneticDisorders), observations);
        MapMedicalConditions(Active(record.MedicalConditions), observations);
        MapMedication(Active(record.Medication), observations);
        MapLifestyle(Active(record.LifestyleFactors), observations);
        MapClinicalTests(Active(record.ClinicalTests), observations);
        MapRhesus(Active(record.RhesusDNegative), observations);
        MapVitalMeasurements(Active(record.VitalMeasurementsBeforePregnancy), observations);
        MapSymphysisFundalHeights(record.SymphysisFundalHeights, observations);
        MapAntenatalAppointments(record.AntenatalAppointments, observations, encounters);

        return new PopulationSnapshot(
            patient,
            observations,
            encounters,
            status.LastChangedDateTime ?? record.Metadata?.RecordLastUpdated,
            status.HasActiveMaternityRecord == true);
    }

    private static void MapCurrentPregnancy(DhgCurrentPregnancy? source, List<PopulationObservation> output)
    {
        if (source is null) return;
        var updated = source.Metadata?.LastUpdated;
        AddDate(output, Id(source.Metadata, "date-last-period"), PopulationCodes.DateLastPeriod, source.DateLastPeriod, updated);
        AddDate(output, Id(source.Metadata, "due-date-last-period"), PopulationCodes.DueDateLastPeriod, source.DueDate, updated);
        AddDate(output, Id(source.Metadata, "due-date-ultrasound"), PopulationCodes.DueDateUltrasound, source.DueDateBasedOnUltrasound, updated);
        AddInteger(output, Id(source.Metadata, "number-of-fetuses"), PopulationCodes.NumberOfFetuses, source.NumberOfFetuses, updated);
        AddBoolean(output, Id(source.Metadata, "assisted-conception"), PopulationCodes.AssistedConception, source.AssistedConception?.HadAssistedConception, updated);
        AddDate(output, Id(source.Metadata, "assisted-conception-date"), PopulationCodes.AssistedConceptionDate, source.AssistedConception?.DateAssistedConception, updated);
        AddBoolean(output, Id(source.Metadata, "prenatal-diagnostics-information"), PopulationCodes.Local("prenatal-diagnostics-information", "Informasjon om fosterdiagnostikk gitt"), source.HasPrenatalDiagnosticsTests, updated);
        AddBoolean(output, Id(source.Metadata, "birth-preparation-talk"), PopulationCodes.Local("birth-preparation-talk", "Fødselsforberedende samtale"), source.BirthPreparationTalk, updated);
        AddBoolean(output, Id(source.Metadata, "breastfeeding-guidance"), PopulationCodes.Local("breastfeeding-guidance", "Ammeveiledning"), source.BreastfeedingGuidance, updated);
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
        AddText(output, Id(source.Metadata, "previous-pregnancy-note"), PopulationCodes.PreviousPregnancyNote, source.Note, updated);
    }

    private static void MapGeneticDisorders(DhgGeneticDisorders? source, List<PopulationObservation> output)
    {
        if (source is null) return;
        var updated = source.Metadata?.LastUpdated;
        AddBoolean(output, Id(source.Metadata, "genetic-none-known"), PopulationCodes.GeneticNoneKnown, source.NoneKnown, updated);
        AddBoolean(output, Id(source.Metadata, "parents-are-relatives"), PopulationCodes.ParentsAreRelatives, source.ParentsAreRelatives, updated);
        AddBoolean(output, Id(source.Metadata, "hip-dysplasia"), PopulationCodes.HipDysplasia, source.HipDysplasia, updated);
        AddBoolean(output, Id(source.Metadata, "other-genetic-disorder"), PopulationCodes.OtherGeneticDisorder, source.Other, updated);
        AddText(output, Id(source.Metadata, "genetic-note"), PopulationCodes.GeneticNote, source.Note, updated);
    }

    private static void MapMedicalConditions(DhgMedicalConditions? source, List<PopulationObservation> output)
    {
        if (source is null) return;
        var updated = source.Metadata?.LastUpdated;
        var fields = new (string Code, string Display, bool? Value)[]
        {
            ("nothing-particular", "Ingenting spesielt", source.NothingParticular),
            ("heart-disease", "Hjertesykdom", source.HeartDisease),
            ("high-blood-pressure", "Hypertensjon", source.HighBloodPressure),
            ("kidney-urinary-tract", "Nyre-/urinveissykdom", source.KidneyUrinaryTractDiseases),
            ("diabetes", "Diabetes eller svangerskapsdiabetes", source.Diabetes),
            ("allergies-asthma", "Allergi og/eller astma", source.AllergiesAsthma),
            ("epilepsy", "Epilepsi", source.Epilepsy),
            ("thrombosis", "Trombose og/eller behandling", source.Thrombosis),
            ("autoimmune-disease", "Autoimmun sykdom", source.AutoimmuneDisease),
            ("gynecological-conditions", "Gynekologiske tilstander/inngrep", source.GynecologicalConditions),
            ("mental-health", "Psykisk helse", source.MentalHealth),
            ("other", "Annen medisinsk tilstand", source.Other)
        };

        foreach (var field in fields)
        {
            AddBoolean(output, Id(source.Metadata, $"medical-{field.Code}"), PopulationCodes.MedicalCondition(field.Code, field.Display), field.Value, updated);
        }

        AddText(output, Id(source.Metadata, "medical-conditions-note"), PopulationCodes.Local("medical-conditions-note", "Merknad om medisinske forhold"), source.Note, updated);
    }

    private static void MapMedication(DhgMedication? source, List<PopulationObservation> output)
    {
        if (source is null) return;
        var updated = source.Metadata?.LastUpdated;
        if (!string.IsNullOrWhiteSpace(source.MedicationFrequency))
        {
            output.Add(Observation(
                Id(source.Metadata, "medication-frequency"),
                PopulationCodes.MedicationFrequency,
                new CodedValue(PopulationCodes.System, source.MedicationFrequency, source.MedicationFrequency),
                "therapy",
                updated,
                note: source.Note));
        }
        AddBoolean(output, Id(source.Metadata, "drug-allergy"), PopulationCodes.DrugAllergy, source.DrugAllergy, updated);
        AddBoolean(output, Id(source.Metadata, "folate-before"), PopulationCodes.FolateBefore, source.Folate?.TakenBefore, updated);
        AddBoolean(output, Id(source.Metadata, "folate-during"), PopulationCodes.FolateDuring, source.Folate?.TakenDuring, updated);
    }

    private static void MapLifestyle(DhgLifestyleFactors? source, List<PopulationObservation> output)
    {
        if (source?.Stimuli is null) return;
        var updated = source.Metadata?.LastUpdated;
        var index = 0;
        foreach (var item in source.Stimuli)
        {
            index++;
            if (item.StimuliType?.Code is null) continue;
            var type = ToCodedValue(item.StimuliType)!;
            var code = new PopulationCode(type.System, type.Code, type.Display ?? type.Code);
            var components = new List<PopulationComponent>();
            AddStimulusFrequency(components, "first-consultation", "Frekvens ved første konsultasjon", item.FirstConsultation);
            AddStimulusFrequency(components, "week-36", "Frekvens ved uke 36", item.AtWeek36);
            output.Add(Observation(
                Id(source.Metadata, $"lifestyle-{item.StimuliType.Code}-{index}"),
                code,
                type,
                "social-history",
                updated,
                components: components,
                note: source.Note));
        }
    }

    private static void AddStimulusFrequency(
        List<PopulationComponent> components,
        string suffix,
        string display,
        DhgStimuliFrequency? source)
    {
        var frequency = ToCodedValue(source?.Frequency);
        if (frequency is not null)
        {
            components.Add(new PopulationComponent(PopulationCodes.Local($"stimuli-frequency-{suffix}", display), frequency));
        }
        if (source?.DailyCount is not null)
        {
            components.Add(new PopulationComponent(PopulationCodes.Local($"stimuli-daily-count-{suffix}", $"Daglig antall {suffix}"), new IntegerValue(source.DailyCount.Value)));
        }
    }

    private static void MapClinicalTests(DhgClinicalTests? source, List<PopulationObservation> output)
    {
        if (source is null) return;
        var updated = source.Metadata?.LastUpdated;
        AddQuantity(output, Id(source.Metadata, "hemoglobin"), PopulationCodes.Hemoglobin, source.Hemoglobin, "g/dL", "g/dL", updated);
        AddQuantity(output, Id(source.Metadata, "hemoglobin-3trimester"), PopulationCodes.HemoglobinThirdTrimester, source.HemoglobinAtThirdTrimester, "g/dL", "g/dL", updated);
        AddQuantity(output, Id(source.Metadata, "ferritin"), PopulationCodes.Ferritin, source.Ferritin, "µg/L", "ug/L", updated);
        AddBooleanLab(output, Id(source.Metadata, "hbv"), PopulationCodes.Hbv, source.Hbv, updated);
        AddBooleanLab(output, Id(source.Metadata, "hbv-core"), PopulationCodes.HbvCore, source.HbvCore, updated);
        AddBooleanLab(output, Id(source.Metadata, "hiv"), PopulationCodes.Hiv, source.Hiv, updated);
        AddBooleanLab(output, Id(source.Metadata, "syphilis"), PopulationCodes.Syphilis, source.Syphilis, updated);
        AddCoded(output, Id(source.Metadata, "abo-type"), PopulationCodes.AboType, source.AboRh?.AboType, updated);
        AddCoded(output, Id(source.Metadata, "rhesus-d-type"), PopulationCodes.RhesusDType, source.AboRh?.RhesusDType, updated);
        AddBooleanLab(output, Id(source.Metadata, "blood-antibodies"), PopulationCodes.BloodAntibodies, source.BloodAntibodies, updated);
        AddBooleanLab(output, Id(source.Metadata, "chlamydia"), PopulationCodes.Chlamydia, source.Chlamydia, updated);
        AddBooleanLab(output, Id(source.Metadata, "toxoplasmosis"), PopulationCodes.Toxoplasmosis, source.Toxoplasmosis, updated);
        AddBooleanLab(output, Id(source.Metadata, "rubella"), PopulationCodes.Rubella, source.RubellaAntigen, updated);
        AddBooleanLab(output, Id(source.Metadata, "hepatitis-c"), PopulationCodes.HepatitisC, source.HepatitisC, updated);
        AddBooleanLab(output, Id(source.Metadata, "mrsa-vre-esbl"), PopulationCodes.MrsaVreEsbl, source.MrsaVreEsbl, updated);
        AddQuantity(output, Id(source.Metadata, "hba1c"), PopulationCodes.HbA1c, source.BHbA1c, "mmol/mol", "mmol/mol", updated);
        AddQuantity(output, Id(source.Metadata, "glucose-fasting"), PopulationCodes.GlucoseFasting, source.GlucoseTolerance?.FastingGlucoseLevel, "mmol/L", "mmol/L", updated, source.GlucoseTolerance?.TestDate);
        AddQuantity(output, Id(source.Metadata, "glucose-2h"), PopulationCodes.Glucose2Hour, source.GlucoseTolerance?.PostTwoHourGlucoseLevel, "mmol/L", "mmol/L", updated, source.GlucoseTolerance?.TestDate);
        AddBooleanLab(output, Id(source.Metadata, "gonorrhea"), PopulationCodes.Gonorrhea, source.Gonorrhea, updated);
        AddBooleanLab(output, Id(source.Metadata, "cytomegalovirus"), PopulationCodes.Cytomegalovirus, source.CytomegaloVirus, updated);
        AddBooleanLab(output, Id(source.Metadata, "abu"), PopulationCodes.AsymptomaticBacteriuria, source.AsymptomaticBacteriuria, updated);
        AddBooleanLab(output, Id(source.Metadata, "gbs"), PopulationCodes.GroupBStreptococci, source.GroupBStreptococci, updated);
    }

    private static void MapRhesus(DhgRhesusDNegative? source, List<PopulationObservation> output)
    {
        if (source is null) return;
        var updated = source.Metadata?.LastUpdated;
        var effective = source.DateForResult is null ? null : new EffectiveDate(source.DateForResult.Value);
        AddBoolean(output, Id(source.Metadata, "rhd-consent"), PopulationCodes.RhesusConsent, source.ConsentFetalRhesusTyping, updated);
        AddBoolean(output, Id(source.Metadata, "fetus-rhd-week24"), PopulationCodes.FetusRhesusWeek24, source.FetusRhDPositiveAtWeek24, updated, "laboratory", effective);
        AddDate(output, Id(source.Metadata, "fetus-rhd-result-date"), PopulationCodes.FetusRhesusResultDate, source.DateForResult, updated);
        AddBoolean(output, Id(source.Metadata, "rhd-prophylaxis-week28"), PopulationCodes.RhesusProphylaxisWeek28, source.ProphylaxisAtWeek28, updated);
    }

    private static void MapVitalMeasurements(DhgVitalMeasurementsBeforePregnancy? source, List<PopulationObservation> output)
    {
        if (source is null) return;
        var updated = source.Metadata?.LastUpdated;
        AddQuantity(output, Id(source.Metadata, "height"), PopulationCodes.Height, source.Height, "cm", "cm", updated, category: "vital-signs");
        AddQuantity(output, Id(source.Metadata, "pre-pregnancy-weight"), PopulationCodes.PrePregnancyWeight, source.PrePregnancyWeight, "kg", "kg", updated, category: "vital-signs");
        if (source.BMI is not null)
        {
            output.Add(Observation(Id(source.Metadata, "bmi"), PopulationCodes.PrePregnancyBmi, new DecimalValue(source.BMI.Value), "vital-signs", updated));
        }
    }

    private static void MapSymphysisFundalHeights(IEnumerable<DhgSymphysisFundalHeight>? sources, List<PopulationObservation> output)
    {
        if (sources is null) return;
        var index = 0;
        foreach (var source in sources.Where(x => x.Metadata?.EnteredInError != true))
        {
            index++;
            if (source.Measurement is null) continue;
            var components = source.PregnancyWeek is null
                ? null
                : new[] { new PopulationComponent(PopulationCodes.GestationalWeeks, new IntegerValue(source.PregnancyWeek.Value)) };
            output.Add(Observation(
                Id(source.Metadata, $"sfh-{index}"),
                PopulationCodes.SymphysisFundalHeight,
                new QuantityValue(source.Measurement.Value, "cm", PopulationCodes.Ucum, "cm"),
                "vital-signs",
                source.Metadata?.LastUpdated,
                source.MeasurementDate is null ? null : new EffectiveDate(source.MeasurementDate.Value),
                components));
        }
    }

    private static void MapAntenatalAppointments(
        IEnumerable<DhgAntenatalAppointment>? sources,
        List<PopulationObservation> output,
        List<PopulationEncounter> encounters)
    {
        if (sources is null) return;
        var appointments = sources
            .Where(x => x.Metadata?.EnteredInError != true)
            .OrderBy(x => x.AppointmentDate)
            .ToList();
        var latestWithGestationalAge = appointments.LastOrDefault(x =>
            x.AppointmentDate is not null &&
            (x.PregnancyWeek is not null || x.DaysAfterFullPregnancyWeek is not null));
        var index = 0;
        foreach (var source in appointments)
        {
            index++;
            if (source.AppointmentDate is null) continue;
            var encounterId = Id(source.Metadata, $"antenatal-{index}");
            var effective = new EffectiveDate(source.AppointmentDate.Value);
            encounters.Add(new PopulationEncounter(encounterId, source.AppointmentDate.Value, source.Metadata?.LastUpdated));

            if (source.PregnancyWeek is not null || source.DaysAfterFullPregnancyWeek is not null)
            {
                var components = new List<PopulationComponent>();
                if (source.PregnancyWeek is not null) components.Add(new(PopulationCodes.GestationalWeeks, new IntegerValue(source.PregnancyWeek.Value)));
                if (source.DaysAfterFullPregnancyWeek is not null) components.Add(new(PopulationCodes.GestationalDays, new IntegerValue(source.DaysAfterFullPregnancyWeek.Value)));
                var text = $"{source.PregnancyWeek?.ToString(CultureInfo.InvariantCulture) ?? "?"}+{source.DaysAfterFullPregnancyWeek?.ToString(CultureInfo.InvariantCulture) ?? "?"}";
                output.Add(Observation(Id(source.Metadata, $"gestational-age-{index}"), PopulationCodes.GestationalAgeAtAppointment, new TextValue(text), "survey", source.Metadata?.LastUpdated, effective, components, encounterId));
                if (ReferenceEquals(source, latestWithGestationalAge))
                {
                    output.Add(Observation(Id(source.Metadata, "recorded-gestational-age"), PopulationCodes.RecordedGestationalAge, new TextValue(text), "survey", source.Metadata?.LastUpdated, effective, components, encounterId));
                }
            }

            AddQuantity(output, Id(source.Metadata, $"mother-weight-{index}"), PopulationCodes.MotherWeight, source.MotherWeight, "kg", "kg", source.Metadata?.LastUpdated, source.AppointmentDate, encounterId, "vital-signs");
            MapBloodPressure(source, output, effective, encounterId, index);
            if (!string.IsNullOrWhiteSpace(source.ProteinInUrineTestResult))
            {
                output.Add(Observation(Id(source.Metadata, $"urine-protein-{index}"), PopulationCodes.UrineProtein, new CodedValue(PopulationCodes.Volven8340, source.ProteinInUrineTestResult, source.ProteinInUrineTestResult), "laboratory", source.Metadata?.LastUpdated, effective, encounterId: encounterId));
            }
            AddInteger(output, Id(source.Metadata, $"edema-{index}"), PopulationCodes.Edema, source.Edema, source.Metadata?.LastUpdated, "exam", effective, encounterId);
            MapFetusVitalSigns(source, output, effective, encounterId, index);
        }
    }

    private static void MapBloodPressure(DhgAntenatalAppointment source, List<PopulationObservation> output, EffectiveDate effective, string encounterId, int index)
    {
        if (string.IsNullOrWhiteSpace(source.BloodPressure)) return;
        var match = BloodPressurePattern().Match(source.BloodPressure);
        if (!match.Success ||
            !int.TryParse(match.Groups[1].Value, CultureInfo.InvariantCulture, out var systolic) ||
            !int.TryParse(match.Groups[2].Value, CultureInfo.InvariantCulture, out var diastolic)) return;

        output.Add(Observation(
            Id(source.Metadata, $"blood-pressure-{index}"),
            PopulationCodes.BloodPressure,
            new TextValue(source.BloodPressure),
            "vital-signs",
            source.Metadata?.LastUpdated,
            effective,
            [
                new PopulationComponent(PopulationCodes.Systolic, new QuantityValue(systolic, "mmHg", PopulationCodes.Ucum, "mm[Hg]")),
                new PopulationComponent(PopulationCodes.Diastolic, new QuantityValue(diastolic, "mmHg", PopulationCodes.Ucum, "mm[Hg]"))
            ],
            encounterId));
    }

    private static void MapFetusVitalSigns(DhgAntenatalAppointment source, List<PopulationObservation> output, EffectiveDate effective, string encounterId, int appointmentIndex)
    {
        if (source.FetusesVitalSigns is null) return;
        var fetusIndex = 0;
        foreach (var fetus in source.FetusesVitalSigns)
        {
            fetusIndex++;
            var suffix = $"{appointmentIndex}-{fetus.FetusId ?? fetusIndex}";
            if (fetus.FetalHeartRate is not null)
            {
                output.Add(Observation(
                    Id(source.Metadata, $"fetal-heart-rate-{suffix}"),
                    PopulationCodes.FetalHeartRate,
                    new QuantityValue(fetus.FetalHeartRate.Value, "beats/minute", PopulationCodes.Ucum, "/min"),
                    "vital-signs",
                    source.Metadata?.LastUpdated,
                    effective,
                    encounterId: encounterId));
            }
            var presentation = ToCodedValue(fetus.FetalPresentationLie);
            if (presentation is not null)
            {
                output.Add(Observation(Id(source.Metadata, $"fetal-presentation-{suffix}"), PopulationCodes.FetalPresentationLie, presentation, "exam", source.Metadata?.LastUpdated, effective, encounterId: encounterId));
            }
            AddBoolean(output, Id(source.Metadata, $"mother-feels-movements-{suffix}"), PopulationCodes.MotherFeelsMovements, fetus.MotherFeelsBabyMovements, source.Metadata?.LastUpdated, "exam", effective, encounterId);
        }
    }

    private static void AddBooleanLab(List<PopulationObservation> output, string id, PopulationCode code, bool? value, DateTimeOffset? updated) =>
        AddBoolean(output, id, code, value, updated, "laboratory");

    private static void AddBoolean(List<PopulationObservation> output, string id, PopulationCode code, bool? value, DateTimeOffset? updated, string category = "survey", PopulationEffective? effective = null, string? encounterId = null)
    {
        if (value is not null) output.Add(Observation(id, code, new BooleanValue(value.Value), category, updated, effective, encounterId: encounterId));
    }

    private static void AddInteger(List<PopulationObservation> output, string id, PopulationCode code, int? value, DateTimeOffset? updated, string category = "survey", PopulationEffective? effective = null, string? encounterId = null)
    {
        if (value is not null) output.Add(Observation(id, code, new IntegerValue(value.Value), category, updated, effective, encounterId: encounterId));
    }

    private static void AddDate(List<PopulationObservation> output, string id, PopulationCode code, DateOnly? value, DateTimeOffset? updated)
    {
        if (value is not null) output.Add(Observation(id, code, new DateValue(value.Value), "survey", updated));
    }

    private static void AddText(List<PopulationObservation> output, string id, PopulationCode code, string? value, DateTimeOffset? updated)
    {
        if (!string.IsNullOrWhiteSpace(value)) output.Add(Observation(id, code, new TextValue(value), "survey", updated));
    }

    private static void AddCoded(List<PopulationObservation> output, string id, PopulationCode code, string? value, DateTimeOffset? updated)
    {
        if (!string.IsNullOrWhiteSpace(value)) output.Add(Observation(id, code, new CodedValue(PopulationCodes.System, value, value), "laboratory", updated));
    }

    private static void AddQuantity<T>(List<PopulationObservation> output, string id, PopulationCode code, T? value, string unit, string unitCode, DateTimeOffset? updated, DateOnly? effectiveDate = null, string? encounterId = null, string category = "laboratory")
        where T : struct, IConvertible
    {
        if (value is null) return;
        var numeric = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
        output.Add(Observation(id, code, new QuantityValue(numeric, unit, PopulationCodes.Ucum, unitCode), category, updated, effectiveDate is null ? null : new EffectiveDate(effectiveDate.Value), encounterId: encounterId));
    }

    private static PopulationObservation Observation(string id, PopulationCode code, PopulationValue value, string category, DateTimeOffset? updated, PopulationEffective? effective = null, IReadOnlyList<PopulationComponent>? components = null, string? encounterId = null, string? note = null) =>
        new(id, code, value, category, updated, effective, components, encounterId, note);

    private static T? Active<T>(T? source) where T : class
    {
        if (source is null) return null;
        var metadata = source.GetType().GetProperty("Metadata")?.GetValue(source) as DhgResourceMetadata;
        return metadata?.EnteredInError == true ? null : source;
    }

    private static CodedValue? ToCodedValue(DhgCodeAndSystem? source)
    {
        if (source?.Code is null) return null;
        return new CodedValue(NormalizeCodeSystem(source.CodeSystem), source.Code, source.Display);
    }

    private static string NormalizeCodeSystem(string? codeSystem)
    {
        if (string.IsNullOrWhiteSpace(codeSystem)) return PopulationCodes.System;
        return codeSystem switch
        {
            "VOLVEN_3303" => PopulationCodes.Volven3303,
            "VOLVEN_8340" => PopulationCodes.Volven8340,
            "VOLVEN_8534" => PopulationCodes.Volven8534,
            "VOLVEN_8536" => PopulationCodes.Volven8536,
            "VOLVEN_8537" => PopulationCodes.Volven8537,
            _ when OidPattern().IsMatch(codeSystem) => $"urn:oid:{codeSystem}",
            _ => codeSystem
        };
    }

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
