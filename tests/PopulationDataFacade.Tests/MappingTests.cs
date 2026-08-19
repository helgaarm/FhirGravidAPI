using Hl7.Fhir.Model;
using PopulationDataFacade.Core;
using PopulationDataFacade.Infrastructure.Dhg;
using Xunit;

namespace PopulationDataFacade.Tests;

public sealed class MappingTests
{
    private static readonly DhgStatusResponse ActiveStatus = new()
    {
        HasGivenConsent = true,
        HasActiveMaternityRecord = true,
        LatestRecordId = "0f0b2f66-34f2-490b-a089-aaa6aa4c9825"
    };

    [Fact]
    public void False_is_mapped_but_null_is_omitted()
    {
        var snapshot = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            ClinicalTests = new DhgClinicalTests
            {
                Metadata = ResourceMetadata("tests"),
                Hbv = null,
                Hiv = false
            }
        });

        var hiv = Assert.Single(snapshot.Observations, x => x.Code == PopulationCodes.Hiv);
        Assert.False(Assert.IsType<BooleanValue>(hiv.Value).Value);
        Assert.DoesNotContain(snapshot.Observations, x => x.Code == PopulationCodes.Hbv);
    }

    [Fact]
    public void Entered_in_error_resource_is_excluded()
    {
        var snapshot = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            ClinicalTests = new DhgClinicalTests
            {
                Metadata = ResourceMetadata("tests", enteredInError: true),
                Hiv = true
            }
        });

        Assert.Empty(snapshot.Observations);
    }

    [Fact]
    public void Previous_pregnancy_fields_are_not_used_to_infer_induced_abortion()
    {
        var snapshot = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            PreviousPregnancies = new DhgPreviousPregnancies
            {
                Metadata = ResourceMetadata("history"),
                NumberOfPreviousPregnancies = 4,
                NumberOfPreviousLiveBirths = 1,
                SpontaneousMiscarriages = 1,
                StillBirths22Weeks = 1,
                NumberOfEctopicPregnancies = 0
            }
        });

        Assert.DoesNotContain(snapshot.Observations, x => x.Code.Code.Contains("induced", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Blood_pressure_is_mapped_to_typed_components()
    {
        var snapshot = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            AntenatalAppointments =
            [
                new DhgAntenatalAppointment
                {
                    Metadata = ResourceMetadata("appointment"),
                    AppointmentDate = new DateOnly(2026, 1, 16),
                    BloodPressure = "118/76"
                }
            ]
        });

        var pressure = Assert.Single(snapshot.Observations, x => x.Code == PopulationCodes.BloodPressure);
        Assert.Equal(2, pressure.Components?.Count);
        Assert.Equal(118m, Assert.IsType<QuantityValue>(pressure.Components![0].Value).Value);
        Assert.Equal(76m, Assert.IsType<QuantityValue>(pressure.Components[1].Value).Value);
    }

    [Fact]
    public void Fhir_mapper_preserves_boolean_type_and_search_filter()
    {
        var snapshot = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            ClinicalTests = new DhgClinicalTests
            {
                Metadata = ResourceMetadata("tests"),
                Hiv = false,
                Hbv = true
            }
        });
        var mapper = new FhirPopulationMapper();

        var result = mapper.MapObservations(snapshot, PopulationCodes.Hiv);

        var observation = Assert.Single(result);
        Assert.False(Assert.IsType<FhirBoolean>(observation.Value).Value);
        Assert.NotNull(observation.Subject);
        Assert.Equal("Patient/patient-1", observation.Subject.Reference);
        Assert.Equal(ObservationStatus.Unknown, observation.Status);
    }

    [Fact]
    public void Medication_note_never_becomes_a_medication_resource_or_name()
    {
        var snapshot = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            Medication = new DhgMedication
            {
                Metadata = ResourceMetadata("medication"),
                Note = "Metoprolol 50 mg",
                DrugAllergy = false
            }
        });

        Assert.DoesNotContain(snapshot.Observations, x => x.Value is TextValue text && text.Value.Contains("Metoprolol", StringComparison.Ordinal));
        Assert.Single(snapshot.Observations, x => x.Code == PopulationCodes.DrugAllergy);
    }

    [Fact]
    public void Latest_gestational_age_is_single_while_appointment_history_is_preserved()
    {
        var snapshot = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            AntenatalAppointments =
            [
                new DhgAntenatalAppointment
                {
                    Metadata = ResourceMetadata("first"),
                    AppointmentDate = new DateOnly(2026, 1, 2),
                    PregnancyWeek = 10,
                    DaysAfterFullPregnancyWeek = 4
                },
                new DhgAntenatalAppointment
                {
                    Metadata = ResourceMetadata("latest"),
                    AppointmentDate = new DateOnly(2026, 1, 9),
                    PregnancyWeek = 11,
                    DaysAfterFullPregnancyWeek = 4
                },
                new DhgAntenatalAppointment
                {
                    Metadata = ResourceMetadata("entered-in-error", enteredInError: true),
                    AppointmentDate = new DateOnly(2026, 1, 16),
                    PregnancyWeek = 12,
                    DaysAfterFullPregnancyWeek = 4
                }
            ]
        });

        Assert.Equal(2, snapshot.Observations.Count(x => x.Code == PopulationCodes.GestationalAgeAtAppointment));
        var latest = Assert.Single(snapshot.Observations, x => x.Code == PopulationCodes.RecordedGestationalAge);
        Assert.Equal("11+4", Assert.IsType<TextValue>(latest.Value).Value);
        Assert.Equal(new DateOnly(2026, 1, 9), Assert.IsType<EffectiveDate>(latest.Effective).Value);
    }

    [Fact]
    public void Ambiguous_lab_concepts_use_facade_codes_and_verified_hemoglobin_units()
    {
        var snapshot = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            ClinicalTests = new DhgClinicalTests
            {
                Metadata = ResourceMetadata("tests"),
                Hemoglobin = 12.4m,
                HemoglobinAtThirdTrimester = 11.9m,
                Hbv = false,
                HbvCore = true,
                Toxoplasmosis = false
            }
        });

        var hemoglobin = Assert.Single(snapshot.Observations, x => x.Code == PopulationCodes.Hemoglobin);
        var thirdTrimester = Assert.Single(snapshot.Observations, x => x.Code == PopulationCodes.HemoglobinThirdTrimester);
        Assert.Equal("g/dL", Assert.IsType<QuantityValue>(hemoglobin.Value).Code);
        Assert.Equal("g/dL", Assert.IsType<QuantityValue>(thirdTrimester.Value).Code);
        Assert.Equal(PopulationCodes.System, hemoglobin.Code.System);
        Assert.Equal(PopulationCodes.System, thirdTrimester.Code.System);
        Assert.Equal(PopulationCodes.System, PopulationCodes.Hbv.System);
        Assert.Equal(PopulationCodes.System, PopulationCodes.HbvCore.System);
        Assert.Equal(PopulationCodes.System, PopulationCodes.Toxoplasmosis.System);
    }

    [Fact]
    public void Fhir_mapper_does_not_infer_patient_or_encounter_status()
    {
        var mapper = new FhirPopulationMapper();
        var updated = DateTimeOffset.Parse("2026-01-16T12:30:00+01:00");
        var snapshot = new PopulationSnapshot(
            new PopulationPatient("patient-1", null, null, updated),
            [],
            [new PopulationEncounter("encounter-1", new DateOnly(2026, 1, 16), updated)],
            updated,
            true);

        var patient = mapper.MapPatient(snapshot.Patient);
        var encounter = Assert.Single(mapper.MapEncounters(snapshot));

        Assert.Null(patient.Active);
        Assert.Equal(Encounter.EncounterStatus.Unknown, encounter.Status);
    }

    private static PopulationSnapshot Create(DhgMaternityRecord record) =>
        new DhgPopulationSnapshotFactory().Create("patient-1", ActiveStatus, record);

    private static DhgRecordMetadata Metadata() => new()
    {
        RecordId = ActiveStatus.LatestRecordId,
        RecordStatus = new DhgRecordStatus { Status = "ACTIVE" }
    };

    private static DhgResourceMetadata ResourceMetadata(string id, bool enteredInError = false) => new()
    {
        Id = id,
        EnteredInError = enteredInError,
        LastUpdated = DateTimeOffset.Parse("2026-01-16T12:30:00+01:00")
    };
}
