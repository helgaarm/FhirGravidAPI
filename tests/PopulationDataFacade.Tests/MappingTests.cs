using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using PopulationDataFacade.Core;
using PopulationDataFacade.Infrastructure.Dhg;
using System.Text.Json.Nodes;
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
                Hbv = false,
                Hiv = true
            }
        });

        var hbv = Assert.Single(snapshot.Observations, x => x.Code == PopulationCodes.Hbv);
        var result = Assert.IsType<CodedValue>(hbv.Value);
        Assert.Equal(PopulationCodes.Volven8340, result.System);
        Assert.Equal("T008", result.Code);
        Assert.Equal("Negativ", result.Display);
        Assert.DoesNotContain(snapshot.Observations, x => x.Id.Contains("hiv", StringComparison.Ordinal));
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
    public void Appointment_without_date_is_ignored_without_partial_resources()
    {
        var snapshot = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            AntenatalAppointments =
            [
                new DhgAntenatalAppointment
                {
                    Metadata = ResourceMetadata("appointment-without-date"),
                    PregnancyWeek = 12,
                    MotherWeight = 67.5m,
                    FetusesVitalSigns = []
                }
            ]
        });

        Assert.Empty(snapshot.Encounters);
        Assert.Empty(snapshot.Observations);
    }

    [Fact]
    public void Empty_nested_fetus_list_preserves_the_appointment_without_fetal_facts()
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
                    FetusesVitalSigns = []
                }
            ]
        });

        Assert.Single(snapshot.Encounters);
        Assert.Empty(snapshot.Observations);
    }

    [Fact]
    public void Fetal_facts_are_omitted_until_fetus_identity_can_be_represented_as_fhir_focus()
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
                    FetusesVitalSigns =
                    [
                        new DhgFetusVitalSigns
                        {
                            FetusId = 1,
                            FetalHeartRate = 145,
                            MotherFeelsBabyMovements = true,
                            FetalPresentationLie = new DhgCodeAndSystem
                            {
                                Code = "1",
                                Display = "Hodeleie",
                                CodeSystem = "VOLVEN_8534"
                            }
                        }
                    ]
                }
            ]
        });

        Assert.Single(snapshot.Encounters);
        Assert.Empty(snapshot.Observations);
    }

    [Fact]
    public void Malformed_measurement_text_is_omitted_without_discarding_valid_appointment_data()
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
                    BloodPressure = "118 by 76",
                    MotherWeight = 67.5m
                }
            ],
            SymphysisFundalHeights =
            [
                new DhgSymphysisFundalHeight
                {
                    Metadata = ResourceMetadata("sfh-without-measurement"),
                    MeasurementDate = new DateOnly(2026, 1, 16),
                    Measurement = null
                }
            ]
        });

        Assert.Single(snapshot.Encounters);
        Assert.Single(snapshot.Observations, observation => observation.Code == PopulationCodes.MotherWeight);
        Assert.DoesNotContain(snapshot.Observations, observation => observation.Code == PopulationCodes.BloodPressure);
        Assert.DoesNotContain(snapshot.Observations, observation => observation.Code == PopulationCodes.SymphysisFundalHeight);
    }

    [Fact]
    public void Corrected_due_date_is_not_mapped_without_an_explicit_clinical_decision()
    {
        var correctedDate = new DateOnly(2026, 5, 12);
        var snapshot = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            CurrentPregnancy = new DhgCurrentPregnancy
            {
                Metadata = ResourceMetadata("pregnancy"),
                DueDate = new DateOnly(2026, 5, 8),
                DueDateBasedOnUltrasound = new DateOnly(2026, 5, 10),
                DueDateCorrectedDate = correctedDate
            }
        });

        Assert.Single(snapshot.Observations, observation => observation.Code == PopulationCodes.DueDateLastPeriod);
        Assert.Single(snapshot.Observations, observation => observation.Code == PopulationCodes.DueDateUltrasound);
        Assert.DoesNotContain(snapshot.Observations, observation =>
            observation.Value is DateValue date && date.Value == correctedDate);
    }

    [Fact]
    public void Assisted_conception_fields_remain_unsupported_without_a_verified_national_code()
    {
        var dateOnly = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            CurrentPregnancy = new DhgCurrentPregnancy
            {
                Metadata = ResourceMetadata("date-only"),
                AssistedConception = new DhgAssistedConception
                {
                    HadAssistedConception = null,
                    DateAssistedConception = new DateOnly(2025, 8, 15)
                }
            }
        });
        var statusOnly = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            CurrentPregnancy = new DhgCurrentPregnancy
            {
                Metadata = ResourceMetadata("status-only"),
                AssistedConception = new DhgAssistedConception
                {
                    HadAssistedConception = true,
                    DateAssistedConception = null
                }
            }
        });
        var statusAndDate = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            CurrentPregnancy = new DhgCurrentPregnancy
            {
                Metadata = ResourceMetadata("status-and-date"),
                AssistedConception = new DhgAssistedConception
                {
                    HadAssistedConception = true,
                    DateAssistedConception = new DateOnly(2025, 8, 15)
                }
            }
        });

        Assert.DoesNotContain(dateOnly.Observations, observation => observation.Id.Contains("assisted-conception", StringComparison.Ordinal));
        Assert.DoesNotContain(statusOnly.Observations, observation => observation.Id.Contains("assisted-conception", StringComparison.Ordinal));
        Assert.DoesNotContain(statusAndDate.Observations, observation => observation.Id.Contains("assisted-conception", StringComparison.Ordinal));
    }

    [Fact]
    public void Fhir_mapper_preserves_national_coded_lab_result_and_search_filter()
    {
        var snapshot = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            ClinicalTests = new DhgClinicalTests
            {
                Metadata = ResourceMetadata("tests"),
                Hbv = false
            }
        });
        var mapper = new FhirPopulationMapper();

        var result = mapper.MapObservations(snapshot, PopulationCodes.Hbv);

        var observation = Assert.Single(result);
        var codedResult = Assert.IsType<CodeableConcept>(observation.Value);
        var coding = Assert.Single(codedResult.Coding);
        Assert.Equal(PopulationCodes.Volven8340, coding.System);
        Assert.Equal("T008", coding.Code);
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
    public void Gestational_age_uses_one_standard_quantity_per_dated_appointment()
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

        var gestationalAges = snapshot.Observations
            .Where(x => x.Code == PopulationCodes.GestationalAge)
            .OrderBy(x => Assert.IsType<EffectiveDate>(x.Effective).Value)
            .ToArray();
        Assert.Equal(2, gestationalAges.Length);
        Assert.Equal(74m, Assert.IsType<QuantityValue>(gestationalAges[0].Value).Value);
        var latest = gestationalAges[1];
        Assert.Equal(81m, Assert.IsType<QuantityValue>(latest.Value).Value);
        Assert.Equal("d", Assert.IsType<QuantityValue>(latest.Value).Code);
        Assert.Equal("11+4", latest.Note);
        Assert.Equal(new DateOnly(2026, 1, 9), Assert.IsType<EffectiveDate>(latest.Effective).Value);
    }

    [Fact]
    public void Laboratory_observations_use_verified_nlk_or_snomed_codes_and_ucum_units()
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
                Hiv = true,
                Syphilis = true,
                Chlamydia = true,
                Toxoplasmosis = false,
                RubellaAntigen = true,
                HepatitisC = true,
                AboRh = new DhgAboRh
                {
                    AboType = "AB",
                    RhesusDType = "NEGATIVE"
                }
            }
        });

        var hemoglobins = snapshot.Observations.Where(x => x.Code == PopulationCodes.Hemoglobin).ToArray();
        Assert.Equal(2, hemoglobins.Length);
        Assert.All(hemoglobins, hemoglobin =>
        {
            Assert.Equal("g/dL", Assert.IsType<QuantityValue>(hemoglobin.Value).Code);
            Assert.Equal(PopulationCodes.Nlk, hemoglobin.Code.System);
            Assert.Equal("NOR05172", hemoglobin.Code.Code);
        });
        Assert.Contains(hemoglobins, hemoglobin => hemoglobin.Note == "Tredje trimester");
        Assert.Equal(PopulationCodes.SnomedCt, PopulationCodes.Hbv.System);
        Assert.Equal("T008", Assert.IsType<CodedValue>(Assert.Single(snapshot.Observations, x => x.Code == PopulationCodes.Hbv).Value).Code);
        Assert.DoesNotContain(snapshot.Observations, observation =>
            new[] { "hbv-core", "hiv", "syphilis", "chlamydia", "toxoplasmosis", "rubella", "hepatitis-c" }
                .Any(name => observation.Id.Contains(name, StringComparison.Ordinal)));

        var abo = Assert.Single(snapshot.Observations, x => x.Code == PopulationCodes.AboType);
        Assert.Equal(PopulationCodes.Nlk, abo.Code.System);
        Assert.Equal("NPU58582", abo.Code.Code);
        var rhesusD = Assert.Single(snapshot.Observations, x => x.Code == PopulationCodes.RhesusDType);
        Assert.Equal(PopulationCodes.Nlk, rhesusD.Code.System);
        Assert.Equal("NPU21917", rhesusD.Code.Code);

        var fhirObservations = new FhirPopulationMapper().MapObservations(snapshot);
        var fhirAbo = Assert.Single(fhirObservations, observation => observation.Id == abo.Id);
        Assert.Contains(fhirAbo.Code.Coding, coding => coding.System == PopulationCodes.Loinc && coding.Code == "883-9");
        var fhirRhesusD = Assert.Single(fhirObservations, observation => observation.Id == rhesusD.Id);
        Assert.Contains(fhirRhesusD.Code.Coding, coding => coding.System == PopulationCodes.Loinc && coding.Code == "10331-7");
        Assert.All(fhirObservations, observation =>
            Assert.Empty(observation.Meta?.Profile ?? []));
    }

    [Fact]
    public void Fhir_uses_standard_codings_and_ucum_quantity_types_without_facade_codes()
    {
        var snapshot = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            CurrentPregnancy = new DhgCurrentPregnancy
            {
                Metadata = ResourceMetadata("pregnancy"),
                DateLastPeriod = new DateOnly(2025, 8, 1),
                DueDate = new DateOnly(2026, 5, 8),
                DueDateBasedOnUltrasound = new DateOnly(2026, 5, 10)
            },
            AntenatalAppointments =
            [
                new DhgAntenatalAppointment
                {
                    Metadata = ResourceMetadata("appointment"),
                    AppointmentDate = new DateOnly(2026, 1, 16),
                    BloodPressure = "118/76",
                    MotherWeight = 67.5m
                }
            ]
        });
        var mapper = new FhirPopulationMapper();
        var observations = mapper.MapObservations(snapshot);

        var lmp = Assert.Single(observations, observation => observation.Code.Coding.Any(coding => coding.Code == "8665-2"));
        Assert.Single(lmp.Code.Coding);
        Assert.Equal(PopulationCodes.Loinc, lmp.Code.Coding[0].System);
        Assert.Equal(2, observations.Count(observation => observation.Code.Coding.Any(coding => coding.Code == "11778-8")));
        Assert.Contains(observations, observation => observation.Code.Coding.Any(coding => coding.System == PopulationCodes.SnomedCt && coding.Code == "289206005"));
        Assert.Contains(observations, observation => observation.Code.Coding.Any(coding => coding.System == PopulationCodes.SnomedCt && coding.Code == "738070007"));

        var appointmentWeight = Assert.Single(observations, observation =>
            observation.Encounter is not null &&
            observation.Code.Coding.Any(coding => coding.Code == "29463-7"));
        Assert.Contains(appointmentWeight.Code.Coding, coding =>
            coding.System == PopulationCodes.SnomedCt && coding.Code == "27113001");
        Assert.Empty(appointmentWeight.Meta?.Profile ?? []);

        var pressure = Assert.Single(observations, observation => observation.Code.Coding.Any(coding => coding.Code == "85354-9"));
        Assert.Null(pressure.Value);
        Assert.Empty(pressure.Meta?.Profile ?? []);
        Assert.Contains(pressure.Component, component =>
            component.Code.Coding.Any(coding => coding.Code == "8480-6") &&
            component.Code.Coding.Any(coding =>
                coding.System == PopulationCodes.SnomedCt && coding.Code == "4471000202106"));
        Assert.Contains(pressure.Component, component =>
            component.Code.Coding.Any(coding => coding.Code == "8462-4") &&
            component.Code.Coding.Any(coding =>
                coding.System == PopulationCodes.SnomedCt && coding.Code == "4481000202108"));

        var byStandardCode = mapper.MapObservations(snapshot, new PopulationCode(PopulationCodes.Loinc, "85354-9", string.Empty));
        Assert.Single(byStandardCode);
        Assert.DoesNotContain(observations.SelectMany(observation => observation.Code.Coding), coding => coding.System?.StartsWith("urn:nhn:", StringComparison.Ordinal) == true);
        Assert.All(observations, observation => Assert.Empty(observation.Meta?.Profile ?? []));
        Assert.All(observations, observation =>
            Assert.DoesNotContain("\"profile\"", new FhirJsonSerializer().SerializeToString(observation), StringComparison.Ordinal));
    }

    [Fact]
    public void Undated_pre_pregnancy_vital_measurements_are_omitted()
    {
        var snapshot = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            VitalMeasurementsBeforePregnancy = new DhgVitalMeasurementsBeforePregnancy
            {
                Metadata = ResourceMetadata("vitals"),
                Height = 168m,
                PrePregnancyWeight = 62.5m,
                BMI = 22.1m
            }
        });

        Assert.Empty(snapshot.Observations);
    }

    [Fact]
    public void Observation_search_filters_category_and_effective_date()
    {
        var updated = DateTimeOffset.Parse("2026-01-16T12:30:00+01:00");
        var snapshot = new PopulationSnapshot(
            new PopulationPatient("patient-1", null, null, updated),
            [
                new PopulationObservation(
                    "lab",
                    PopulationCodes.Hemoglobin,
                    new QuantityValue(12.4m, "g/dL", PopulationCodes.Ucum, "g/dL"),
                    "laboratory",
                    updated),
                new PopulationObservation(
                    "weight-1",
                    PopulationCodes.MotherWeight,
                    new QuantityValue(67.5m, "kg", PopulationCodes.Ucum, "kg"),
                    "vital-signs",
                    updated,
                    new EffectiveDate(new DateOnly(2026, 1, 16))),
                new PopulationObservation(
                    "weight-2",
                    PopulationCodes.MotherWeight,
                    new QuantityValue(68m, "kg", PopulationCodes.Ucum, "kg"),
                    "vital-signs",
                    updated,
                    new EffectiveDate(new DateOnly(2026, 1, 20)))
            ],
            [],
            updated,
            true);
        var mapper = new FhirPopulationMapper();

        var result = mapper.MapObservations(snapshot, new PopulationObservationSearch(
            CategorySystem: "http://terminology.hl7.org/CodeSystem/observation-category",
            CategoryCode: "vital-signs",
            Date: new PopulationDateSearch(
                PopulationDateComparison.GreaterThanOrEqual,
                new DateOnly(2026, 1, 18))));
        var unknownSystem = mapper.MapObservations(snapshot, new PopulationObservationSearch(
            CategorySystem: "urn:unknown",
            CategoryCode: "vital-signs"));

        Assert.Equal("weight-2", Assert.Single(result).Id);
        Assert.Empty(unknownSystem);
    }

    [Fact]
    public void Urine_protein_enum_is_mapped_to_national_text_result_codes_and_unknown_is_omitted()
    {
        var values = new[] { "Neg", "Spor", "1+", "2+", "3+", "Ukjent" };
        var appointments = values.Select((value, index) => new DhgAntenatalAppointment
        {
            Metadata = ResourceMetadata($"appointment-{index}"),
            AppointmentDate = new DateOnly(2026, 1, 1).AddDays(index),
            ProteinInUrineTestResult = value
        }).ToList();
        var snapshot = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            AntenatalAppointments = appointments
        });

        var results = snapshot.Observations
            .Where(observation => observation.Code == PopulationCodes.UrineProtein)
            .Select(observation => Assert.IsType<CodedValue>(observation.Value))
            .ToArray();

        Assert.Equal(5, results.Length);
        Assert.Equal(["T008", "T052", "T048", "T049", "T050"], results.Select(result => result.Code));
        Assert.All(results, result => Assert.Equal(PopulationCodes.Volven8340, result.System));
    }

    [Fact]
    public void Fhir_observation_dates_serialize_as_date_time_with_day_precision()
    {
        var snapshot = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            CurrentPregnancy = new DhgCurrentPregnancy
            {
                Metadata = ResourceMetadata("pregnancy"),
                DateLastPeriod = new DateOnly(2025, 8, 1)
            },
            AntenatalAppointments =
            [
                new DhgAntenatalAppointment
                {
                    Metadata = ResourceMetadata("appointment"),
                    AppointmentDate = new DateOnly(2026, 1, 16),
                    MotherWeight = 67.5m
                }
            ]
        });
        var observations = new FhirPopulationMapper().MapObservations(snapshot);
        var serializer = new FhirJsonSerializer();

        var lmp = Assert.Single(observations, observation =>
            observation.Code.Coding.Any(coding => coding.Code == "8665-2"));
        Assert.Equal("2025-08-01", Assert.IsType<FhirDateTime>(lmp.Value).Value);
        var lmpJson = serializer.SerializeToString(lmp);
        Assert.Contains("\"valueDateTime\":\"2025-08-01\"", lmpJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"valueDate\":", lmpJson, StringComparison.Ordinal);

        var weight = Assert.Single(observations, observation => observation.Encounter is not null);
        Assert.Equal("2026-01-16", Assert.IsType<FhirDateTime>(weight.Effective).Value);
        var weightJson = serializer.SerializeToString(weight);
        Assert.Contains("\"effectiveDateTime\":\"2026-01-16\"", weightJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"effectiveDate\":", weightJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Contract_invalid_measurements_ranges_and_unsupported_scales_are_omitted()
    {
        var snapshot = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            CurrentPregnancy = new DhgCurrentPregnancy
            {
                Metadata = ResourceMetadata("pregnancy"),
                NumberOfFetuses = 0
            },
            ClinicalTests = new DhgClinicalTests
            {
                Metadata = ResourceMetadata("tests"),
                Hemoglobin = 0,
                Ferritin = -1,
                BHbA1c = 0,
                GlucoseTolerance = new DhgGlucoseTolerance
                {
                    FastingGlucoseLevel = -1,
                    PostTwoHourGlucoseLevel = 0
                }
            },
            VitalMeasurementsBeforePregnancy = new DhgVitalMeasurementsBeforePregnancy
            {
                Metadata = ResourceMetadata("vitals"),
                Height = 0,
                PrePregnancyWeight = -1,
                BMI = 0
            },
            SymphysisFundalHeights =
            [
                new DhgSymphysisFundalHeight
                {
                    Metadata = ResourceMetadata("sfh"),
                    MeasurementDate = new DateOnly(2026, 1, 16),
                    Measurement = 0
                }
            ],
            AntenatalAppointments =
            [
                new DhgAntenatalAppointment
                {
                    Metadata = ResourceMetadata("appointment"),
                    AppointmentDate = new DateOnly(2026, 1, 16),
                    PregnancyWeek = 0,
                    MotherWeight = -1,
                    BloodPressure = "00/00",
                    Edema = 2
                }
            ]
        });

        Assert.Single(snapshot.Encounters);
        Assert.Empty(snapshot.Observations);
    }

    [Fact]
    public void Capability_statement_uses_the_standard_R4_base_resource_without_profile_claims()
    {
        var capability = new FhirPopulationMapper().CapabilityStatement(new Uri("https://localhost/"));
        var observation = Assert.Single(
            Assert.Single(capability.Rest).Resource,
            resource => resource.Type == ResourceType.Observation.ToString());

        Assert.Null(observation.Profile);
        Assert.Empty(observation.SupportedProfile);
        Assert.Contains(observation.SearchParam, parameter => parameter.Name == "category");
        Assert.Contains(observation.SearchParam, parameter => parameter.Name == "date");
    }

    [Fact]
    public void Standard_R4_validation_examples_match_production_mapper_output()
    {
        var updated = DateTimeOffset.Parse("2026-01-16T12:30:00+01:00");
        var snapshot = new PopulationSnapshot(
            new PopulationPatient("patient-1", null, null, updated),
            [
                new PopulationObservation(
                    "date-value",
                    PopulationCodes.DateLastPeriod,
                    new DateValue(new DateOnly(2025, 8, 1)),
                    "survey",
                    updated),
                new PopulationObservation(
                    "boolean-value",
                    PopulationCodes.BirthPreparationTalk,
                    new BooleanValue(true),
                    "survey",
                    updated),
                new PopulationObservation(
                    "coded-value",
                    PopulationCodes.Hbv,
                    new CodedValue(PopulationCodes.Volven8340, "T002", "Positiv"),
                    "laboratory",
                    updated),
                new PopulationObservation(
                    "body-weight",
                    PopulationCodes.MotherWeight,
                    new QuantityValue(67.5m, "kg", PopulationCodes.Ucum, "kg"),
                    "vital-signs",
                    updated,
                    new EffectiveDate(new DateOnly(2026, 1, 16)),
                    EncounterId: "encounter-1"),
                new PopulationObservation(
                    "blood-pressure",
                    PopulationCodes.BloodPressure,
                    null,
                    "vital-signs",
                    updated,
                    new EffectiveDate(new DateOnly(2026, 1, 16)),
                    [
                        new PopulationComponent(
                            PopulationCodes.Systolic,
                            new QuantityValue(118m, "mmHg", PopulationCodes.Ucum, "mm[Hg]")),
                        new PopulationComponent(
                            PopulationCodes.Diastolic,
                            new QuantityValue(76m, "mmHg", PopulationCodes.Ucum, "mm[Hg]"))
                    ],
                    "encounter-1")
            ],
            [new PopulationEncounter("encounter-1", new DateOnly(2026, 1, 16), updated)],
            updated,
            true);
        var mapper = new FhirPopulationMapper();

        AssertValidationExample("Patient.json", mapper.MapPatient(snapshot.Patient));
        AssertValidationExample("Encounter.json", Assert.Single(mapper.MapEncounters(snapshot)));
        foreach (var observation in mapper.MapObservations(snapshot))
            AssertValidationExample($"Observation-{observation.Id}.json", observation);
    }

    [Fact]
    public void Published_static_codes_and_patient_extension_are_standard_or_national()
    {
        var allowedSystems = new HashSet<string>(StringComparer.Ordinal)
        {
            PopulationCodes.Loinc,
            PopulationCodes.SnomedCt,
            PopulationCodes.Nlk,
            PopulationCodes.Volven3303,
            PopulationCodes.Volven8340,
            PopulationCodes.Volven8536,
            PopulationCodes.Volven8537
        };
        var publishedCodes = typeof(PopulationCodes)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(field => field.FieldType == typeof(PopulationCode))
            .Select(field => Assert.IsType<PopulationCode>(field.GetValue(null)))
            .ToArray();

        Assert.NotEmpty(publishedCodes);
        Assert.All(publishedCodes, code => Assert.Contains(code.System, allowedSystems));

        var patient = new FhirPopulationMapper().MapPatient(
            new PopulationPatient("patient-1", null, true, null));
        var extension = Assert.Single(patient.Extension);
        Assert.Equal("http://hl7.org/fhir/StructureDefinition/patient-interpreterRequired", extension.Url);
    }

    [Fact]
    public void Ambiguous_fields_are_unsupported_instead_of_receiving_guessed_or_facade_codes()
    {
        var snapshot = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            CurrentPregnancy = new DhgCurrentPregnancy
            {
                Metadata = ResourceMetadata("pregnancy"),
                HasPrenatalDiagnosticsTests = true
            },
            GeneticDisorders = new DhgGeneticDisorders
            {
                Metadata = ResourceMetadata("genetics"),
                HipDysplasia = true,
                Other = true,
                Note = "Skal ikke tolkes"
            },
            MedicalConditions = new DhgMedicalConditions
            {
                Metadata = ResourceMetadata("medical"),
                KidneyUrinaryTractDiseases = true,
                AllergiesAsthma = true,
                GynecologicalConditions = true,
                Other = true,
                Note = "Skal ikke tolkes"
            },
            Medication = new DhgMedication
            {
                Metadata = ResourceMetadata("medication"),
                MedicationFrequency = "DAILY",
                Note = "Skal ikke tolkes"
            },
            ClinicalTests = new DhgClinicalTests
            {
                Metadata = ResourceMetadata("tests"),
                MrsaVreEsbl = true,
                Gonorrhea = true,
                CytomegaloVirus = true
            },
            RhesusDNegative = new DhgRhesusDNegative
            {
                Metadata = ResourceMetadata("rhesus"),
                ConsentFetalRhesusTyping = true,
                FetusRhDPositiveAtWeek24 = true,
                DateForResult = new DateOnly(2026, 1, 16)
            }
        });

        Assert.Empty(snapshot.Observations);
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

    private static void AssertValidationExample(string fileName, Resource resource)
    {
        var expectedPath = Path.Combine(AppContext.BaseDirectory, "validation", fileName);
        var expected = JsonNode.Parse(File.ReadAllText(expectedPath));
        var actualJson = new FhirJsonSerializer().SerializeToString(resource);
        var actual = JsonNode.Parse(actualJson);

        Assert.True(
            JsonNode.DeepEquals(expected, actual),
            $"Generated FHIR resource did not match R4 validation example {fileName}. Actual: {actualJson}");
    }

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
