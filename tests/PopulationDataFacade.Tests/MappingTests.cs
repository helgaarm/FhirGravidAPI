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
    public void Laboratory_false_is_negative_true_is_positive_and_null_is_omitted()
    {
        var snapshot = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            ClinicalTests = new DhgClinicalTests
            {
                Metadata = ResourceMetadata("tests"),
                Hbv = false,
                HbvCore = true,
                BloodAntibodies = false,
                RubellaAntigen = true,
                Hiv = true,
                Syphilis = null
            }
        });

        var hbv = Assert.Single(snapshot.Observations, x => x.Code == PopulationCodes.Hbv);
        var result = Assert.IsType<CodedValue>(hbv.Value);
        Assert.Equal(PopulationCodes.Volven8340, result.System);
        Assert.Equal("T008", result.Code);
        Assert.Equal("Negativ", result.Display);

        var hiv = Assert.Single(snapshot.Observations, x => x.Code == PopulationCodes.HivTestResult);
        Assert.False(hiv.Code.HasCoding);
        var hivResult = Assert.IsType<CodedValue>(hiv.Value);
        Assert.Equal(PopulationCodes.Volven8340, hivResult.System);
        Assert.Equal("T002", hivResult.Code);
        Assert.Equal("Positiv", hivResult.Display);

        Assert.Equal("T002", Assert.IsType<CodedValue>(Assert.Single(
            snapshot.Observations,
            x => x.Code == PopulationCodes.HbvCoreAntibodyTestResult).Value).Code);
        Assert.Equal("T008", Assert.IsType<CodedValue>(Assert.Single(
            snapshot.Observations,
            x => x.Code == PopulationCodes.BloodTypeAntibodyTestResult).Value).Code);
        Assert.Equal("T002", Assert.IsType<CodedValue>(Assert.Single(
            snapshot.Observations,
            x => x.Code == PopulationCodes.RubellaIgg).Value).Code);
        Assert.DoesNotContain(snapshot.Observations, x => x.Code == PopulationCodes.SyphilisTestResult);
        Assert.Equal(5, snapshot.Observations.Count);
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
    public void Marked_points_of_contact_are_mapped_to_a_patient_care_team()
    {
        var snapshot = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            PointsOfContact = new DhgPointsOfContact
            {
                Metadata = ResourceMetadata("contacts"),
                GeneralPractitioner = new DhgPersonAndOrganization { Name = "Skal ikke eksponeres" },
                Midwife = new DhgPersonAndOrganization
                {
                    Name = "  Kari Jordmor  ",
                    OrganizationName = "Sentrum jordmortjeneste"
                },
                BirthInstitute = "Skal ikke eksponeres",
                MaternityHealthcareCentre = "  Sentrum helsestasjon  "
            }
        });

        var source = Assert.Single(snapshot.CareTeams!);
        Assert.Equal("Kari Jordmor", source.Midwife?.Name);
        Assert.Equal("Sentrum jordmortjeneste", source.Midwife?.OrganizationName);
        Assert.Equal("Sentrum helsestasjon", source.MaternityHealthcareCentre);

        var careTeam = Assert.Single(new FhirPopulationMapper().MapCareTeams(snapshot));
        Assert.Equal(CareTeam.CareTeamStatus.Active, careTeam.Status);
        Assert.NotNull(careTeam.Subject);
        Assert.Equal("Patient/patient-1", careTeam.Subject.Reference);
        Assert.Equal("Kari Jordmor", Assert.IsType<Practitioner>(careTeam.Contained[0]).Name[0].Text);
        Assert.Equal("Sentrum jordmortjeneste", Assert.IsType<Organization>(careTeam.Contained[1]).Name);
        Assert.Equal("Sentrum helsestasjon", Assert.IsType<Organization>(careTeam.Contained[2]).Name);
        Assert.Equal(2, careTeam.Participant.Count);
        var midwife = careTeam.Participant[0];
        Assert.Equal("Jordmor", Assert.Single(midwife.Role).Text);
        Assert.NotNull(midwife.Member);
        Assert.NotNull(midwife.OnBehalfOf);
        Assert.Equal("#midwife", midwife.Member.Reference);
        Assert.Equal("#midwife-organization", midwife.OnBehalfOf.Reference);
        var healthcareCentre = careTeam.Participant[1];
        Assert.Equal("Helsestasjon", Assert.Single(healthcareCentre.Role).Text);
        Assert.NotNull(healthcareCentre.Member);
        Assert.Equal("#maternity-healthcare-centre", healthcareCentre.Member.Reference);
        Assert.Empty(careTeam.ManagingOrganization);

        var json = new FhirJsonSerializer().SerializeToString(careTeam);
        Assert.DoesNotContain("Skal ikke eksponeres", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Unmarked_or_entered_in_error_points_of_contact_are_not_exposed()
    {
        var unmarkedOnly = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            PointsOfContact = new DhgPointsOfContact
            {
                Metadata = ResourceMetadata("contacts"),
                GeneralPractitioner = new DhgPersonAndOrganization { Name = "Fastlege" },
                BirthInstitute = "Fødeinstitusjon"
            }
        });
        var enteredInError = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            PointsOfContact = new DhgPointsOfContact
            {
                Metadata = ResourceMetadata("contacts", enteredInError: true),
                Midwife = new DhgPersonAndOrganization { Name = "Jordmor" },
                MaternityHealthcareCentre = "Helsestasjon"
            }
        });

        Assert.Empty(unmarkedOnly.CareTeams!);
        Assert.Empty(enteredInError.CareTeams!);
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
                NumberOfEctopicPregnancies = 0,
                Note = "  Ett annet utfall er omtalt uten struktur  "
            }
        });

        Assert.DoesNotContain(snapshot.Observations, x =>
            x.Code.Code?.Contains("induced", StringComparison.OrdinalIgnoreCase) == true);
        var note = Assert.Single(snapshot.Observations, x => x.Code == PopulationCodes.PreviousPregnanciesNote);
        Assert.Equal("Ett annet utfall er omtalt uten struktur", Assert.IsType<TextValue>(note.Value).Value);
        Assert.Contains("tolkes ikke", note.Note, StringComparison.Ordinal);
    }

    [Fact]
    public void Marked_genetic_disorder_fields_are_exposed_without_interpreting_free_text()
    {
        const string sourceNote = "  Mor har en arvelig sykdom som ikke er kodet  ";
        var snapshot = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            GeneticDisorders = new DhgGeneticDisorders
            {
                Metadata = ResourceMetadata("genetics"),
                NoneKnown = false,
                ParentsAreRelatives = true,
                HipDysplasia = true,
                Other = true,
                Note = sourceNote
            }
        });

        Assert.Equal(5, snapshot.Observations.Count);
        Assert.False(Assert.IsType<BooleanValue>(Assert.Single(
            snapshot.Observations,
            observation => observation.Code == PopulationCodes.NoKnownGeneticDisorders).Value).Value);
        Assert.True(Assert.IsType<BooleanValue>(Assert.Single(
            snapshot.Observations,
            observation => observation.Code == PopulationCodes.ParentsAreRelatives).Value).Value);
        Assert.True(Assert.IsType<BooleanValue>(Assert.Single(
            snapshot.Observations,
            observation => observation.Code == PopulationCodes.OtherGeneticDisorder).Value).Value);
        var hipDysplasia = Assert.Single(
            snapshot.Observations,
            observation => observation.Code == PopulationCodes.HipDysplasiaFamilyHistory);
        Assert.True(Assert.IsType<BooleanValue>(hipDysplasia.Value).Value);
        Assert.Contains("berørt person", hipDysplasia.Note, StringComparison.Ordinal);
        var note = Assert.Single(
            snapshot.Observations,
            observation => observation.Code == PopulationCodes.GeneticDisordersNote);
        Assert.Equal(
            "Mor har en arvelig sykdom som ikke er kodet",
            Assert.IsType<TextValue>(note.Value).Value);

        var fhirNote = Assert.Single(
            new FhirPopulationMapper().MapObservations(snapshot),
            observation => observation.Id?.EndsWith("genetic-note", StringComparison.Ordinal) == true);
        Assert.Empty(fhirNote.Code.Coding);
        Assert.Equal("Merknad om arvelige sykdommer", fhirNote.Code.Text);
        Assert.Equal(
            "Mor har en arvelig sykdom som ikke er kodet",
            Assert.IsType<FhirString>(fhirNote.Value).Value);
    }

    [Fact]
    public void Null_or_blank_genetic_fields_are_omitted()
    {
        var snapshot = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            GeneticDisorders = new DhgGeneticDisorders
            {
                Metadata = ResourceMetadata("genetics"),
                HipDysplasia = null,
                Note = "   "
            }
        });

        Assert.Empty(snapshot.Observations);
    }

    [Fact]
    public void Composite_medical_fields_and_note_are_source_preserving_with_explicit_limitations()
    {
        const string sourceNote = "  Tidligere operert; nærmere diagnose er ikke oppgitt  ";
        var snapshot = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            MedicalConditions = new DhgMedicalConditions
            {
                Metadata = ResourceMetadata("medical"),
                NothingParticular = false,
                KidneyUrinaryTractDiseases = true,
                AllergiesAsthma = false,
                GynecologicalConditions = true,
                Other = true,
                Note = sourceNote
            }
        });

        Assert.Equal(6, snapshot.Observations.Count);
        Assert.False(Assert.IsType<BooleanValue>(Assert.Single(snapshot.Observations,
            observation => observation.Code == PopulationCodes.NothingParticularMedical).Value).Value);
        Assert.True(Assert.IsType<BooleanValue>(Assert.Single(snapshot.Observations,
            observation => observation.Code == PopulationCodes.KidneyOrUrinaryTractDisease).Value).Value);
        Assert.False(Assert.IsType<BooleanValue>(Assert.Single(snapshot.Observations,
            observation => observation.Code == PopulationCodes.AllergyOrAsthma).Value).Value);
        Assert.True(Assert.IsType<BooleanValue>(Assert.Single(snapshot.Observations,
            observation => observation.Code == PopulationCodes.GynecologicalConditionOrIntervention).Value).Value);
        Assert.True(Assert.IsType<BooleanValue>(Assert.Single(snapshot.Observations,
            observation => observation.Code == PopulationCodes.OtherMedicalCondition).Value).Value);

        var note = Assert.Single(snapshot.Observations,
            observation => observation.Code == PopulationCodes.MedicalConditionsNote);
        Assert.Equal("Tidligere operert; nærmere diagnose er ikke oppgitt", Assert.IsType<TextValue>(note.Value).Value);
        Assert.All(snapshot.Observations, observation =>
        {
            Assert.False(observation.Code.HasCoding);
            Assert.False(string.IsNullOrWhiteSpace(observation.Note));
        });

        var fhir = new FhirPopulationMapper().MapObservations(snapshot);
        Assert.All(fhir, observation =>
        {
            Assert.Empty(observation.Code.Coding);
            Assert.Single(observation.Note);
        });
        var fhirNote = Assert.Single(fhir, observation => observation.Id == note.Id);
        Assert.Equal(
            "Tidligere operert; nærmere diagnose er ikke oppgitt",
            Assert.IsType<FhirString>(fhirNote.Value).Value);

        var omitted = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            MedicalConditions = new DhgMedicalConditions
            {
                Metadata = ResourceMetadata("medical-empty"),
                Note = "   "
            }
        });
        Assert.Empty(omitted.Observations);
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
                    FetusesVitalSigns =
                    [
                        new DhgFetusVitalSigns
                        {
                            FetusId = 1,
                            FetalHeartRate = 145
                        }
                    ]
                }
            ]
        });

        Assert.Empty(snapshot.Encounters);
        Assert.Empty(snapshot.Observations);
        Assert.Empty(snapshot.Fetuses!);
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
    public void Fetal_facts_use_pregnancy_scoped_patients_as_fhir_focus()
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
                            Note = "  Normale funn  ",
                            FetalPresentationLie = new DhgCodeAndSystem
                            {
                                Code = "1",
                                Display = "Hodeleie",
                                CodeSystem = "VOLVEN_8534"
                            }
                        },
                        new DhgFetusVitalSigns
                        {
                            FetusId = 2,
                            FetalHeartRate = 150,
                            MotherFeelsBabyMovements = false
                        }
                    ]
                }
            ]
        });

        Assert.Single(snapshot.Encounters);
        Assert.Equal(2, snapshot.Fetuses!.Count);
        Assert.Equal(6, snapshot.Observations.Count);

        var firstFetusId = FetalPatientId.Create("patient-1", 1);
        var secondFetusId = FetalPatientId.Create("patient-1", 2);
        Assert.Matches("^fetus-[a-f0-9]{40}$", firstFetusId);
        Assert.NotEqual(firstFetusId, secondFetusId);
        Assert.NotEqual(firstFetusId, FetalPatientId.Create("patient-2", 1));
        Assert.All(snapshot.Observations.Where(observation => observation.Id.Contains("-1-1", StringComparison.Ordinal)),
            observation => Assert.Equal(firstFetusId, observation.FocusPatientId));
        Assert.All(snapshot.Observations.Where(observation => observation.Id.Contains("-2-1", StringComparison.Ordinal)),
            observation => Assert.Equal(secondFetusId, observation.FocusPatientId));

        var heartRate = Assert.Single(snapshot.Observations, observation =>
            observation.Code == PopulationCodes.FetalHeartRate &&
            observation.FocusPatientId == firstFetusId);
        var heartRateValue = Assert.IsType<QuantityValue>(heartRate.Value);
        Assert.Equal(145m, heartRateValue.Value);
        Assert.Equal("{beats}/min", heartRateValue.Code);
        Assert.Equal("vital-signs", heartRate.Category);

        var presentation = Assert.Single(snapshot.Observations, observation =>
            observation.Code == PopulationCodes.FetalPresentationLie);
        var presentationValue = Assert.IsType<CodedValue>(presentation.Value);
        Assert.Equal(PopulationCodes.Volven8534, presentationValue.System);
        Assert.Equal("1", presentationValue.Code);

        Assert.False(Assert.IsType<BooleanValue>(Assert.Single(snapshot.Observations, observation =>
            observation.Code == PopulationCodes.FetalMovementsReported &&
            observation.FocusPatientId == secondFetusId).Value).Value);
        Assert.Equal("Normale funn", Assert.IsType<TextValue>(Assert.Single(snapshot.Observations, observation =>
            observation.Code == PopulationCodes.FetalFindingsNote).Value).Value);

        var mapper = new FhirPopulationMapper();
        var fetalPatients = mapper.MapFetusPatients(snapshot);
        Assert.Equal(2, fetalPatients.Count);
        Assert.All(fetalPatients, patient =>
        {
            Assert.Empty(patient.Identifier);
            Assert.Empty(patient.Name);
            Assert.Null(patient.GenderElement);
            Assert.Null(patient.BirthDateElement);
        });
        var fhirHeartRate = Assert.Single(mapper.MapObservations(snapshot), observation =>
            observation.Id == heartRate.Id);
        Assert.NotNull(fhirHeartRate.Subject);
        Assert.Equal("Patient/patient-1", fhirHeartRate.Subject.Reference);
        Assert.Equal($"Patient/{firstFetusId}", Assert.Single(fhirHeartRate.Focus).Reference);
        Assert.Contains(fhirHeartRate.Code.Coding, coding =>
            coding.System == PopulationCodes.SnomedCt && coding.Code == "364075005");
        Assert.Contains(fhirHeartRate.Code.Coding, coding =>
            coding.System == PopulationCodes.Loinc && coding.Code == "55283-6");
    }

    [Fact]
    public void Invalid_or_unidentified_fetal_findings_are_not_exposed()
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
                        new DhgFetusVitalSigns { FetusId = null, FetalHeartRate = 145 },
                        new DhgFetusVitalSigns { FetusId = 0, MotherFeelsBabyMovements = true },
                        new DhgFetusVitalSigns
                        {
                            FetusId = 1,
                            FetalHeartRate = 0,
                            FetalPresentationLie = new DhgCodeAndSystem
                            {
                                Code = "1",
                                Display = "Hodeleie",
                                CodeSystem = "urn:unsupported"
                            },
                            Note = "   "
                        }
                    ]
                }
            ]
        });

        Assert.Single(snapshot.Encounters);
        Assert.Single(snapshot.Fetuses!);
        Assert.Empty(snapshot.Observations);
    }

    [Fact]
    public void Repeated_source_fetus_id_reuses_patient_and_keeps_newest_timestamp()
    {
        var firstMetadata = ResourceMetadata("appointment-1");
        firstMetadata.LastUpdated = DateTimeOffset.Parse("2026-01-10T12:00:00+01:00");
        var latestMetadata = ResourceMetadata("appointment-2");
        latestMetadata.LastUpdated = DateTimeOffset.Parse("2026-01-20T12:00:00+01:00");
        var snapshot = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            AntenatalAppointments =
            [
                new DhgAntenatalAppointment
                {
                    Metadata = firstMetadata,
                    AppointmentDate = new DateOnly(2026, 1, 10),
                    FetusesVitalSigns = [new DhgFetusVitalSigns { FetusId = 1, FetalHeartRate = 140 }]
                },
                new DhgAntenatalAppointment
                {
                    Metadata = latestMetadata,
                    AppointmentDate = new DateOnly(2026, 1, 20),
                    FetusesVitalSigns = [new DhgFetusVitalSigns { FetusId = 1, FetalHeartRate = 145 }]
                }
            ]
        });

        var fetus = Assert.Single(snapshot.Fetuses!);
        Assert.Equal(latestMetadata.LastUpdated, fetus.LastUpdated);
        var heartRates = snapshot.Observations
            .Where(observation => observation.Code == PopulationCodes.FetalHeartRate)
            .ToArray();
        Assert.Equal(2, heartRates.Length);
        Assert.All(heartRates, observation => Assert.Equal(fetus.LogicalId, observation.FocusPatientId));
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
    public void Corrected_due_date_is_preserved_without_selecting_a_clinically_preferred_due_date()
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
        var corrected = Assert.Single(snapshot.Observations, observation => observation.Code == PopulationCodes.CorrectedDueDate);
        Assert.Equal(correctedDate, Assert.IsType<DateValue>(corrected.Value).Value);
        Assert.False(corrected.Code.HasCoding);
    }

    [Fact]
    public void Assisted_conception_date_does_not_infer_status_and_status_does_not_infer_date()
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
        var falseStatusAndDate = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            CurrentPregnancy = new DhgCurrentPregnancy
            {
                Metadata = ResourceMetadata("false-status-and-date"),
                AssistedConception = new DhgAssistedConception
                {
                    HadAssistedConception = false,
                    DateAssistedConception = new DateOnly(2025, 8, 15)
                }
            }
        });

        Assert.DoesNotContain(dateOnly.Observations, observation => observation.Id.Contains("assisted-conception", StringComparison.Ordinal));
        var trueObservation = Assert.Single(statusOnly.Observations, observation => observation.Code == PopulationCodes.AssistedConception);
        Assert.True(Assert.IsType<BooleanValue>(trueObservation.Value).Value);
        Assert.Null(trueObservation.Effective);

        var falseObservation = Assert.Single(falseStatusAndDate.Observations, observation => observation.Code == PopulationCodes.AssistedConception);
        Assert.False(Assert.IsType<BooleanValue>(falseObservation.Value).Value);
        Assert.Null(falseObservation.Effective);
    }

    [Fact]
    public void Assisted_conception_maps_verified_norwegian_snomed_code_and_explicit_date()
    {
        var expectedDate = new DateOnly(2025, 8, 15);
        var snapshot = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            CurrentPregnancy = new DhgCurrentPregnancy
            {
                Metadata = ResourceMetadata("status-and-date"),
                AssistedConception = new DhgAssistedConception
                {
                    HadAssistedConception = true,
                    DateAssistedConception = expectedDate
                }
            }
        });

        var source = Assert.Single(snapshot.Observations, observation => observation.Code == PopulationCodes.AssistedConception);
        Assert.True(Assert.IsType<BooleanValue>(source.Value).Value);
        Assert.Equal(expectedDate, Assert.IsType<EffectiveDate>(source.Effective).Value);

        var fhir = Assert.Single(new FhirPopulationMapper().MapObservations(snapshot, PopulationCodes.AssistedConception));
        var coding = Assert.Single(fhir.Code.Coding);
        Assert.Equal(PopulationCodes.SnomedCt, coding.System);
        Assert.Equal("813541000000100", coding.Code);
        Assert.Equal("svangerskap ved assistert befruktning", coding.Display);
        Assert.True(Assert.IsType<FhirBoolean>(fhir.Value).Value);
        Assert.Equal("2025-08-15", Assert.IsType<FhirDateTime>(fhir.Effective).Value);
    }

    [Fact]
    public void Prenatal_diagnostics_information_preserves_true_false_and_null_without_test_inference()
    {
        static PopulationSnapshot Snapshot(bool? value) => Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            CurrentPregnancy = new DhgCurrentPregnancy
            {
                Metadata = ResourceMetadata("pregnancy"),
                HasPrenatalDiagnosticsTests = value
            }
        });

        var positive = Assert.Single(
            Snapshot(true).Observations,
            observation => observation.Code == PopulationCodes.PrenatalDiagnosticsInformationProvided);
        Assert.True(Assert.IsType<BooleanValue>(positive.Value).Value);
        Assert.False(positive.Code.HasCoding);
        Assert.Equal("Gitt informasjon om fosterdiagnostikk", positive.Code.Display);

        var negative = Assert.Single(
            Snapshot(false).Observations,
            observation => observation.Code == PopulationCodes.PrenatalDiagnosticsInformationProvided);
        Assert.False(Assert.IsType<BooleanValue>(negative.Value).Value);

        var fhir = Assert.Single(new FhirPopulationMapper().MapObservations(Snapshot(false)));
        Assert.Empty(fhir.Code.Coding);
        Assert.Equal("Gitt informasjon om fosterdiagnostikk", fhir.Code.Text);
        Assert.False(Assert.IsType<FhirBoolean>(fhir.Value).Value);

        Assert.Empty(Snapshot(null).Observations);
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
    public void Medication_note_is_preserved_as_unparsed_text_instead_of_a_medication_resource_or_name()
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

        var note = Assert.Single(snapshot.Observations, x => x.Code == PopulationCodes.MedicationNote);
        Assert.Equal("Metoprolol 50 mg", Assert.IsType<TextValue>(note.Value).Value);
        Assert.Contains("tolkes ikke", note.Note, StringComparison.Ordinal);
        Assert.Single(snapshot.Observations, x => x.Code == PopulationCodes.DrugAllergy);
    }

    [Fact]
    public void Maternal_household_answers_are_source_preserving_social_history_findings()
    {
        var snapshot = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            Mother = new DhgMother
            {
                Metadata = ResourceMetadata("mother"),
                CohabitingCoparent = false,
                CohabitingCoparentNote = "  Delt bosted er omtalt i kilden  "
            }
        });

        var cohabiting = Assert.Single(snapshot.Observations, x => x.Code == PopulationCodes.CohabitingCoparent);
        Assert.False(Assert.IsType<BooleanValue>(cohabiting.Value).Value);
        Assert.Equal("social-history", cohabiting.Category);
        var note = Assert.Single(snapshot.Observations, x => x.Code == PopulationCodes.CohabitingCoparentNote);
        Assert.Equal("Delt bosted er omtalt i kilden", Assert.IsType<TextValue>(note.Value).Value);
        Assert.Equal("social-history", note.Category);
    }

    [Fact]
    public void Appointment_medication_answer_and_note_are_encounter_scoped_without_inference()
    {
        var date = new DateOnly(2026, 1, 16);
        var snapshot = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            AntenatalAppointments =
            [
                new DhgAntenatalAppointment
                {
                    Metadata = ResourceMetadata("appointment"),
                    AppointmentDate = date,
                    Medication = false,
                    Note = "  Oppfølging avtalt  "
                }
            ]
        });

        var medication = Assert.Single(snapshot.Observations, x => x.Code == PopulationCodes.AntenatalMedicationReported);
        Assert.False(Assert.IsType<BooleanValue>(medication.Value).Value);
        var encounter = Assert.Single(snapshot.Encounters);
        Assert.Equal(encounter.Id, medication.EncounterId);
        Assert.Equal(date, Assert.IsType<EffectiveDate>(medication.Effective).Value);
        var note = Assert.Single(snapshot.Observations, x => x.Code == PopulationCodes.AntenatalAppointmentNote);
        Assert.Equal("Oppfølging avtalt", Assert.IsType<TextValue>(note.Value).Value);
        Assert.Equal(encounter.Id, note.EncounterId);
        Assert.Contains("tolkes ikke", note.Note, StringComparison.Ordinal);
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
    public void Laboratory_observations_use_verified_codings_or_exact_source_text_and_ucum_units()
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
                BloodAntibodies = false,
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

        var rubella = Assert.Single(snapshot.Observations, observation => observation.Code == PopulationCodes.RubellaIgg);
        Assert.Equal(PopulationCodes.Nlk, rubella.Code.System);
        Assert.Equal("NPU12412", rubella.Code.Code);
        Assert.Equal("T002", Assert.IsType<CodedValue>(rubella.Value).Code);

        var fhirObservations = new FhirPopulationMapper().MapObservations(snapshot);
        var broadTestResults = new[]
        {
            (PopulationCodes.HbvCoreAntibodyTestResult, "T002"),
            (PopulationCodes.HivTestResult, "T002"),
            (PopulationCodes.SyphilisTestResult, "T002"),
            (PopulationCodes.BloodTypeAntibodyTestResult, "T008"),
            (PopulationCodes.ChlamydiaTestResult, "T002"),
            (PopulationCodes.ToxoplasmosisTestResult, "T008"),
            (PopulationCodes.HepatitisCTestResult, "T002")
        };
        foreach (var (code, expectedResult) in broadTestResults)
        {
            var source = Assert.Single(snapshot.Observations, observation => observation.Code == code);
            Assert.False(source.Code.HasCoding);
            Assert.Equal(expectedResult, Assert.IsType<CodedValue>(source.Value).Code);

            var fhir = Assert.Single(fhirObservations, observation => observation.Id == source.Id);
            Assert.Empty(fhir.Code.Coding);
            Assert.Equal(code.Display, fhir.Code.Text);
            var fhirResult = Assert.IsType<CodeableConcept>(fhir.Value);
            Assert.Equal(expectedResult, Assert.Single(fhirResult.Coding).Code);
        }

        var abo = Assert.Single(snapshot.Observations, x => x.Code == PopulationCodes.AboType);
        Assert.Equal(PopulationCodes.Nlk, abo.Code.System);
        Assert.Equal("NPU58582", abo.Code.Code);
        var rhesusD = Assert.Single(snapshot.Observations, x => x.Code == PopulationCodes.RhesusDType);
        Assert.Equal(PopulationCodes.Nlk, rhesusD.Code.System);
        Assert.Equal("NPU21917", rhesusD.Code.Code);

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
    public void Undated_pre_pregnancy_measurements_are_base_R4_observations_without_effective_time()
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

        Assert.Equal(3, snapshot.Observations.Count);
        Assert.All(snapshot.Observations, observation =>
        {
            Assert.Equal("vital-signs", observation.Category);
            Assert.Null(observation.Effective);
            Assert.Equal(
                "Før svangerskapet; measurement time er ikke oppgitt av DHG",
                observation.Note);
        });

        var height = Assert.Single(snapshot.Observations, observation =>
            observation.Code == PopulationCodes.BodyHeight);
        Assert.Equal(168m, Assert.IsType<QuantityValue>(height.Value).Value);
        Assert.Equal("cm", Assert.IsType<QuantityValue>(height.Value).Code);
        var weight = Assert.Single(snapshot.Observations, observation =>
            observation.Code == PopulationCodes.MotherWeight);
        Assert.Equal(62.5m, Assert.IsType<QuantityValue>(weight.Value).Value);
        Assert.Equal("kg", Assert.IsType<QuantityValue>(weight.Value).Code);
        var bmi = Assert.Single(snapshot.Observations, observation =>
            observation.Code == PopulationCodes.BodyMassIndex);
        Assert.Equal(22.1m, Assert.IsType<QuantityValue>(bmi.Value).Value);
        Assert.Equal("kg/m2", Assert.IsType<QuantityValue>(bmi.Value).Code);

        var mapper = new FhirPopulationMapper();
        var fhir = mapper.MapObservations(snapshot);
        Assert.All(fhir, observation =>
        {
            Assert.Null(observation.Effective);
            Assert.Empty(observation.Meta?.Profile ?? []);
            Assert.Single(observation.Note);
        });
        Assert.Contains(Assert.Single(fhir, observation => observation.Id == height.Id).Code.Coding,
            coding => coding.System == PopulationCodes.Loinc && coding.Code == "8302-2");
        Assert.Contains(Assert.Single(fhir, observation => observation.Id == bmi.Id).Code.Coding,
            coding => coding.System == PopulationCodes.Loinc && coding.Code == "39156-5");
        Assert.Empty(mapper.MapObservations(snapshot, new PopulationObservationSearch(
            Date: new PopulationDateSearch(
                PopulationDateComparison.GreaterThanOrEqual,
                new DateOnly(2025, 1, 1)))));
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
    public void Contract_invalid_measurements_are_omitted_while_valid_raw_edema_grade_is_preserved()
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
        var edema = Assert.Single(snapshot.Observations);
        Assert.Equal(PopulationCodes.EdemaGrade, edema.Code);
        Assert.Equal(2, Assert.IsType<IntegerValue>(edema.Value).Value);
        Assert.Contains("Rå DHG-grad", edema.Note, StringComparison.Ordinal);
    }

    [Fact]
    public void Daily_stimulus_count_is_a_source_preserving_component_of_the_coded_stimulus()
    {
        var snapshot = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            LifestyleFactors = new DhgLifestyleFactors
            {
                Metadata = ResourceMetadata("lifestyle"),
                Stimuli =
                [
                    new DhgStimulus
                    {
                        StimuliType = new DhgCodeAndSystem
                        {
                            CodeSystem = PopulationCodes.Volven8536,
                            Code = "TOBACCO",
                            Display = "Tobakk"
                        },
                        FirstConsultation = new DhgStimuliFrequency
                        {
                            Frequency = new DhgCodeAndSystem
                            {
                                CodeSystem = PopulationCodes.Volven8537,
                                Code = "DAILY",
                                Display = "Daglig"
                            },
                            DailyCount = 0
                        }
                    }
                ]
            }
        });

        var observation = Assert.Single(snapshot.Observations);
        var component = Assert.Single(observation.Components!);
        Assert.Equal(PopulationCodes.DailyStimulusCount, component.Code);
        Assert.Equal(0, Assert.IsType<IntegerValue>(component.Value).Value);
    }

    [Fact]
    public void Capability_statement_uses_the_standard_R4_base_resource_without_profile_claims()
    {
        var capability = new FhirPopulationMapper().CapabilityStatement(new Uri("https://localhost/"));
        var observation = Assert.Single(
            Assert.Single(capability.Rest).Resource,
            resource => resource.Type == ResourceType.Observation.ToString());
        var careTeam = Assert.Single(
            Assert.Single(capability.Rest).Resource,
            resource => resource.Type == ResourceType.CareTeam.ToString());

        Assert.Null(observation.Profile);
        Assert.Empty(observation.SupportedProfile);
        Assert.Contains(observation.SearchParam, parameter => parameter.Name == "category");
        Assert.Contains(observation.SearchParam, parameter => parameter.Name == "date");
        Assert.Contains(careTeam.SearchParam, parameter => parameter.Name == "patient");
        Assert.Contains(careTeam.SearchParam, parameter => parameter.Name == "patient.identifier");
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
                    "text-code",
                    PopulationCodes.HivTestResult,
                    new CodedValue(PopulationCodes.Volven8340, "T008", "Negativ"),
                    "laboratory",
                    updated),
                new PopulationObservation(
                    "text-value",
                    PopulationCodes.GeneticDisordersNote,
                    new TextValue("Mor har en arvelig sykdom som ikke er kodet"),
                    "survey",
                    updated),
                new PopulationObservation(
                    "pre-pregnancy-height",
                    PopulationCodes.BodyHeight,
                    new QuantityValue(168m, "cm", PopulationCodes.Ucum, "cm"),
                    "vital-signs",
                    updated,
                    Note: "Før svangerskapet; measurement time er ikke oppgitt av DHG"),
                new PopulationObservation(
                    "pre-pregnancy-weight",
                    PopulationCodes.MotherWeight,
                    new QuantityValue(62.5m, "kg", PopulationCodes.Ucum, "kg"),
                    "vital-signs",
                    updated,
                    Note: "Før svangerskapet; measurement time er ikke oppgitt av DHG"),
                new PopulationObservation(
                    "pre-pregnancy-bmi",
                    PopulationCodes.BodyMassIndex,
                    new QuantityValue(22.1m, "kg/m²", PopulationCodes.Ucum, "kg/m2"),
                    "vital-signs",
                    updated,
                    Note: "Før svangerskapet; measurement time er ikke oppgitt av DHG"),
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
                    "encounter-1"),
                new PopulationObservation(
                    "fetal-heart-rate",
                    PopulationCodes.FetalHeartRate,
                    new QuantityValue(145m, "slag/min", PopulationCodes.Ucum, "{beats}/min"),
                    "vital-signs",
                    updated,
                    new EffectiveDate(new DateOnly(2026, 1, 16)),
                    EncounterId: "encounter-1",
                    FocusPatientId: "fetus-1")
            ],
            [new PopulationEncounter("encounter-1", new DateOnly(2026, 1, 16), updated)],
            updated,
            true,
            [
                new PopulationCareTeam(
                    "pregnancy-care-team",
                    new PopulationCareTeamMember("Kari Jordmor", "Sentrum jordmortjeneste"),
                    "Sentrum helsestasjon",
                    updated)
            ],
            [new PopulationFetusPatient("fetus-1", updated)]);
        var mapper = new FhirPopulationMapper();

        AssertValidationExample("Patient.json", mapper.MapPatient(snapshot.Patient));
        AssertValidationExample("Patient-fetus.json", Assert.Single(mapper.MapFetusPatients(snapshot)));
        AssertValidationExample("Encounter.json", Assert.Single(mapper.MapEncounters(snapshot)));
        AssertValidationExample("CareTeam.json", Assert.Single(mapper.MapCareTeams(snapshot)));
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
            PopulationCodes.Volven8534,
            PopulationCodes.Volven8536,
            PopulationCodes.Volven8537
        };
        var publishedCodes = typeof(PopulationCodes)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(field => field.FieldType == typeof(PopulationCode))
            .Select(field => Assert.IsType<PopulationCode>(field.GetValue(null)))
            .ToArray();

        Assert.NotEmpty(publishedCodes);
        Assert.All(publishedCodes, code =>
        {
            Assert.False(string.IsNullOrWhiteSpace(code.Display));
            if (!code.HasCoding)
            {
                Assert.Null(code.System);
                Assert.Null(code.Code);
                return;
            }

            Assert.NotNull(code.System);
            Assert.NotNull(code.Code);
            Assert.Contains(code.System!, allowedSystems);
        });

        var patient = new FhirPopulationMapper().MapPatient(
            new PopulationPatient("patient-1", null, true, null));
        var extension = Assert.Single(patient.Extension);
        Assert.Equal("http://hl7.org/fhir/StructureDefinition/patient-interpreterRequired", extension.Url);
    }

    [Fact]
    public void Broad_or_unparsed_findings_are_preserved_without_guessed_or_facade_codes()
    {
        var snapshot = Create(new DhgMaternityRecord
        {
            Metadata = Metadata(),
            GeneticDisorders = new DhgGeneticDisorders
            {
                Metadata = ResourceMetadata("genetics"),
                HipDysplasia = true
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
                CytomegaloVirus = false,
                AsymptomaticBacteriuria = true,
                GroupBStreptococci = false,
                Note = "Kontrolleres senere"
            },
            RhesusDNegative = new DhgRhesusDNegative
            {
                Metadata = ResourceMetadata("rhesus"),
                ConsentFetalRhesusTyping = true,
                FetusRhDPositiveAtWeek24 = true,
                DateForResult = new DateOnly(2026, 1, 16)
            }
        });

        Assert.Collection(
            snapshot.Observations,
            observation => AssertTextBoolean(observation, PopulationCodes.HipDysplasiaFamilyHistory, true),
            observation => AssertText(observation, PopulationCodes.MedicationFrequency, "DAILY"),
            observation => AssertText(observation, PopulationCodes.MedicationNote, "Skal ikke tolkes"),
            observation => AssertTextLabResult(observation, PopulationCodes.MrsaVreEsblTestResult, "T002"),
            observation => AssertTextLabResult(observation, PopulationCodes.GonorrheaTestResult, "T002"),
            observation => AssertTextLabResult(observation, PopulationCodes.CytomegalovirusTestResult, "T008"),
            observation => AssertTextLabResult(observation, PopulationCodes.AsymptomaticBacteriuriaTestResult, "T002"),
            observation => AssertTextLabResult(observation, PopulationCodes.GroupBStreptococciTestResult, "T008"),
            observation => AssertText(observation, PopulationCodes.ClinicalTestsNote, "Kontrolleres senere"));

        Assert.DoesNotContain(snapshot.Observations, observation =>
            observation.Id.Contains("rhd", StringComparison.Ordinal));
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

    private static void AssertTextBoolean(PopulationObservation observation, PopulationCode code, bool expected)
    {
        Assert.Equal(code, observation.Code);
        Assert.False(observation.Code.HasCoding);
        Assert.Equal(expected, Assert.IsType<BooleanValue>(observation.Value).Value);
    }

    private static void AssertText(PopulationObservation observation, PopulationCode code, string expected)
    {
        Assert.Equal(code, observation.Code);
        Assert.False(observation.Code.HasCoding);
        Assert.Equal(expected, Assert.IsType<TextValue>(observation.Value).Value);
    }

    private static void AssertTextLabResult(PopulationObservation observation, PopulationCode code, string expectedCode)
    {
        Assert.Equal(code, observation.Code);
        Assert.False(observation.Code.HasCoding);
        var value = Assert.IsType<CodedValue>(observation.Value);
        Assert.Equal(PopulationCodes.Volven8340, value.System);
        Assert.Equal(expectedCode, value.Code);
    }

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
