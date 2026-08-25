using Hl7.Fhir.Model;

namespace PopulationDataFacade.Core;

public interface IFhirPopulationMapper
{
    Patient MapPatient(PopulationPatient patient);
    IReadOnlyList<Patient> MapFetusPatients(PopulationSnapshot snapshot);
    IReadOnlyList<Observation> MapObservations(PopulationSnapshot snapshot, PopulationCode? filter = null);
    IReadOnlyList<Observation> MapObservations(PopulationSnapshot snapshot, PopulationObservationSearch search);
    IReadOnlyList<Encounter> MapEncounters(PopulationSnapshot snapshot);
    IReadOnlyList<CareTeam> MapCareTeams(PopulationSnapshot snapshot);
    Bundle SearchBundle(IEnumerable<Resource> resources, Uri? serviceBase = null);
    CapabilityStatement CapabilityStatement(Uri serviceBase);
}

public sealed class FhirPopulationMapper : IFhirPopulationMapper
{
    public Patient MapPatient(PopulationPatient source)
    {
        var patient = new Patient
        {
            Id = source.LogicalId,
            Meta = Meta(source.LastUpdated)
        };

        if (source.PreferredLanguage is not null)
        {
            patient.Communication.Add(new Patient.CommunicationComponent
            {
                Language = ToCodeableConcept(source.PreferredLanguage),
                Preferred = true
            });
        }

        if (source.NeedsInterpreter is not null)
        {
            patient.Extension.Add(new Extension(
                "http://hl7.org/fhir/StructureDefinition/patient-interpreterRequired",
                new FhirBoolean(source.NeedsInterpreter.Value)));
        }

        return patient;
    }

    public IReadOnlyList<Patient> MapFetusPatients(PopulationSnapshot snapshot) =>
        (snapshot.Fetuses ?? [])
            .Select(source => new Patient
            {
                Id = source.LogicalId,
                Meta = Meta(source.LastUpdated)
            })
            .ToArray();

    public IReadOnlyList<Observation> MapObservations(PopulationSnapshot snapshot, PopulationCode? filter = null) =>
        MapObservations(snapshot, new PopulationObservationSearch(Code: filter));

    public IReadOnlyList<Observation> MapObservations(PopulationSnapshot snapshot, PopulationObservationSearch search)
    {
        return snapshot.Observations
            .Where(x => search.Code is null || PopulationCodes.Matches(x.Code, search.Code))
            .Where(x => MatchesCategory(x, search))
            .Where(x => MatchesDate(x.Effective, search.Date))
            .Select(x => MapObservation(snapshot.Patient.LogicalId, x))
            .ToArray();
    }

    public IReadOnlyList<Encounter> MapEncounters(PopulationSnapshot snapshot) => snapshot.Encounters
        .Select(x => new Encounter
        {
            Id = x.Id,
            Meta = Meta(x.LastUpdated),
            Status = Encounter.EncounterStatus.Unknown,
            Class = new Coding("http://terminology.hl7.org/CodeSystem/v3-ActCode", "AMB", "ambulatory"),
            Subject = new ResourceReference($"Patient/{snapshot.Patient.LogicalId}"),
            Period = x.Date is { } date
                ? new Period(new FhirDateTime(date.ToString("yyyy-MM-dd")), new FhirDateTime(date.ToString("yyyy-MM-dd")))
                : null
        })
        .ToArray();

    public IReadOnlyList<CareTeam> MapCareTeams(PopulationSnapshot snapshot) =>
        (snapshot.CareTeams ?? [])
            .Select(x => MapCareTeam(snapshot.Patient.LogicalId, x))
            .ToArray();

    public Bundle SearchBundle(IEnumerable<Resource> resources, Uri? serviceBase = null)
    {
        var materialized = resources.ToArray();
        var bundle = new Bundle
        {
            Type = Bundle.BundleType.Searchset,
            Total = materialized.Length,
            Timestamp = DateTimeOffset.UtcNow
        };

        foreach (var resource in materialized)
        {
            bundle.Entry.Add(new Bundle.EntryComponent
            {
                FullUrl = serviceBase is null
                    ? null
                    : new Uri(serviceBase, $"fhir/{resource.TypeName}/{resource.Id}").ToString(),
                Resource = resource,
                Search = new Bundle.SearchComponent { Mode = Bundle.SearchEntryMode.Match }
            });
        }

        return bundle;
    }

    public CapabilityStatement CapabilityStatement(Uri serviceBase) => new()
    {
        Id = "population-data-facade-capability",
        Url = new Uri(serviceBase, "fhir/metadata").ToString(),
        Version = "1.0.0",
        Name = "PopulationDataFacadeCapabilityStatement",
        Title = "FHIR Population Data Facade for DHG",
        Status = PublicationStatus.Active,
        Experimental = false,
        Date = DateTimeOffset.UtcNow.ToString("O"),
        Publisher = "Norsk helsesektor",
        Kind = CapabilityStatementKind.Instance,
        FhirVersion = FHIRVersion.N4_0_1,
        Format = ["application/fhir+json"],
        Rest =
        [
            new Hl7.Fhir.Model.CapabilityStatement.RestComponent
            {
                Mode = Hl7.Fhir.Model.CapabilityStatement.RestfulCapabilityMode.Server,
                Documentation = "GET operations use a protected logical patient context. POST _search accepts NIN only in an application/x-www-form-urlencoded request body; it requires HelseID outside local DevelopmentTestMode. NIN in a GET URL is not supported.",
                Resource =
                [
                    ResourceCapability(
                        ResourceType.Patient,
                        true,
                        Hl7.Fhir.Model.CapabilityStatement.TypeRestfulInteraction.Read,
                        Hl7.Fhir.Model.CapabilityStatement.TypeRestfulInteraction.SearchType),
                    ResourceCapability(
                        ResourceType.Observation,
                        false,
                        Hl7.Fhir.Model.CapabilityStatement.TypeRestfulInteraction.SearchType),
                    ResourceCapability(
                        ResourceType.Encounter,
                        false,
                        Hl7.Fhir.Model.CapabilityStatement.TypeRestfulInteraction.SearchType),
                    ResourceCapability(
                        ResourceType.CareTeam,
                        false,
                        Hl7.Fhir.Model.CapabilityStatement.TypeRestfulInteraction.SearchType)
                ]
            }
        ]
    };

    private static Observation MapObservation(string patientId, PopulationObservation source)
    {
        var observation = new Observation
        {
            Id = source.Id,
            Meta = Meta(source.LastUpdated),
            Status = ObservationStatus.Unknown,
            Code = ToCodeableConcept(source.Code),
            Subject = new ResourceReference($"Patient/{patientId}"),
            Value = ToDataType(source.Value),
            Category =
            [
                new CodeableConcept(
                    "http://terminology.hl7.org/CodeSystem/observation-category",
                    source.Category,
                    source.Category)
            ]
        };

        observation.Effective = source.Effective switch
        {
            EffectiveDate date => new FhirDateTime(date.Value.ToString("yyyy-MM-dd")),
            EffectiveDateTime instant => new FhirDateTime(instant.Value),
            _ => null
        };

        if (source.Components is not null)
        {
            observation.Component = source.Components.Select(component => new Observation.ComponentComponent
            {
                Code = ToCodeableConcept(component.Code),
                Value = ToDataType(component.Value)
            }).ToList();
        }

        if (source.EncounterId is not null)
        {
            observation.Encounter = new ResourceReference($"Encounter/{source.EncounterId}");
        }

        if (source.FocusPatientId is not null)
        {
            observation.Focus = [new ResourceReference($"Patient/{source.FocusPatientId}")];
        }

        if (!string.IsNullOrWhiteSpace(source.Note))
        {
            observation.Note = [new Annotation { Text = source.Note }];
        }

        return observation;
    }

    private static CareTeam MapCareTeam(string patientId, PopulationCareTeam source)
    {
        var careTeam = new CareTeam
        {
            Id = source.Id,
            Meta = Meta(source.LastUpdated),
            Status = CareTeam.CareTeamStatus.Active,
            Subject = new ResourceReference($"Patient/{patientId}")
        };

        AddPractitionerParticipant(
            careTeam,
            source.GeneralPractitioner,
            "Fastlege",
            "general-practitioner");
        AddPractitionerParticipant(careTeam, source.Midwife, "Jordmor", "midwife");

        if (!string.IsNullOrWhiteSpace(source.MaternityHealthcareCentre))
        {
            careTeam.Contained.Add(new Organization
            {
                Id = "maternity-healthcare-centre",
                Type = [new CodeableConcept { Text = "Helsestasjon" }],
                Name = source.MaternityHealthcareCentre
            });
            careTeam.Participant.Add(new CareTeam.ParticipantComponent
            {
                Member = new ResourceReference(
                    "#maternity-healthcare-centre",
                    source.MaternityHealthcareCentre)
            });
        }

        if (!string.IsNullOrWhiteSpace(source.BirthInstitute))
        {
            careTeam.Contained.Add(new Organization
            {
                Id = "birth-institute",
                Type = [new CodeableConcept { Text = "Fødeinstitusjon" }],
                Name = source.BirthInstitute
            });
            careTeam.Participant.Add(new CareTeam.ParticipantComponent
            {
                Role = [new CodeableConcept { Text = "Fødeinstitusjon" }],
                Member = new ResourceReference("#birth-institute", source.BirthInstitute)
            });
        }

        return careTeam;
    }

    private static void AddPractitionerParticipant(
        CareTeam careTeam,
        PopulationCareTeamMember? source,
        string role,
        string containedId)
    {
        if (source is null) return;

        ResourceReference? practitionerReference = null;
        if (!string.IsNullOrWhiteSpace(source.Name) ||
            !string.IsNullOrWhiteSpace(source.HprNumber))
        {
            var practitioner = new Practitioner
            {
                Id = containedId
            };
            if (!string.IsNullOrWhiteSpace(source.Name))
                practitioner.Name.Add(new HumanName { Text = source.Name });

            if (!string.IsNullOrWhiteSpace(source.HprNumber))
            {
                practitioner.Identifier.Add(new Identifier
                {
                    System = PopulationIdentifierSystems.HprNumber,
                    Value = source.HprNumber
                });
            }

            careTeam.Contained.Add(practitioner);
            practitionerReference = new ResourceReference($"#{containedId}", source.Name);
        }

        ResourceReference? organizationReference = null;
        if (!string.IsNullOrWhiteSpace(source.OrganizationName) ||
            !string.IsNullOrWhiteSpace(source.OrganizationId))
        {
            var organizationId = $"{containedId}-organization";
            var organization = new Organization
            {
                Id = organizationId,
                Name = source.OrganizationName
            };
            if (!string.IsNullOrWhiteSpace(source.OrganizationId))
            {
                organization.Identifier.Add(new Identifier
                {
                    System = PopulationIdentifierSystems.OrganizationNumber,
                    Value = source.OrganizationId
                });
            }

            careTeam.Contained.Add(organization);
            organizationReference = new ResourceReference(
                $"#{organizationId}",
                source.OrganizationName);
        }

        var practitionerRoleId = $"{containedId}-role";
        careTeam.Contained.Add(new PractitionerRole
        {
            Id = practitionerRoleId,
            Practitioner = practitionerReference,
            Organization = organizationReference,
            Code = [new CodeableConcept { Text = role }]
        });
        careTeam.Participant.Add(new CareTeam.ParticipantComponent
        {
            Member = new ResourceReference(
                $"#{practitionerRoleId}",
                source.Name ?? source.OrganizationName)
        });
    }

    private static CapabilityStatement.ResourceComponent ResourceCapability(
        ResourceType resourceType,
        bool includePatientIdentifier,
        params CapabilityStatement.TypeRestfulInteraction[] interactions) => new()
    {
        Type = resourceType.ToString(),
        Interaction = interactions
            .Select(interaction => new CapabilityStatement.ResourceInteractionComponent { Code = interaction })
            .ToList(),
        SearchParam = resourceType is ResourceType.Observation
            ?
            [
                new CapabilityStatement.SearchParamComponent
                {
                    Name = "patient",
                    Type = SearchParamType.Reference,
                    Definition = "http://hl7.org/fhir/SearchParameter/clinical-patient"
                },
                new CapabilityStatement.SearchParamComponent
                {
                    Name = "patient.identifier",
                    Type = SearchParamType.Token
                },
                new CapabilityStatement.SearchParamComponent
                {
                    Name = "code",
                    Type = SearchParamType.Token,
                    Definition = "http://hl7.org/fhir/SearchParameter/clinical-code"
                },
                new CapabilityStatement.SearchParamComponent
                {
                    Name = "category",
                    Type = SearchParamType.Token,
                    Definition = "http://hl7.org/fhir/SearchParameter/Observation-category"
                },
                new CapabilityStatement.SearchParamComponent
                {
                    Name = "date",
                    Type = SearchParamType.Date,
                    Definition = "http://hl7.org/fhir/SearchParameter/clinical-date"
                }
            ]
            : resourceType is ResourceType.Encounter or ResourceType.CareTeam
                ?
                [
                    new CapabilityStatement.SearchParamComponent
                    {
                        Name = "patient",
                        Type = SearchParamType.Reference,
                        Definition = "http://hl7.org/fhir/SearchParameter/clinical-patient"
                    },
                    new CapabilityStatement.SearchParamComponent
                    {
                        Name = "patient.identifier",
                        Type = SearchParamType.Token
                    }
                ]
                : resourceType is ResourceType.Patient && includePatientIdentifier
                    ?
                    [
                        new CapabilityStatement.SearchParamComponent
                        {
                            Name = "identifier",
                            Type = SearchParamType.Token,
                            Definition = "http://hl7.org/fhir/SearchParameter/Patient-identifier"
                        }
                    ]
                    : []
    };

    private static Meta? Meta(DateTimeOffset? lastUpdated) => lastUpdated is null
        ? null
        : new Meta { LastUpdated = lastUpdated };

    private static bool MatchesCategory(PopulationObservation observation, PopulationObservationSearch search)
    {
        if (search.CategoryCode is null) return true;
        const string observationCategorySystem =
            "http://terminology.hl7.org/CodeSystem/observation-category";
        return (search.CategorySystem is null ||
                string.Equals(search.CategorySystem, observationCategorySystem, StringComparison.Ordinal)) &&
               string.Equals(observation.Category, search.CategoryCode, StringComparison.Ordinal);
    }

    private static bool MatchesDate(PopulationEffective? effective, PopulationDateSearch? search)
    {
        if (search is null) return true;
        var value = effective switch
        {
            EffectiveDate date => date.Value,
            EffectiveDateTime instant => DateOnly.FromDateTime(instant.Value.Date),
            _ => (DateOnly?)null
        };
        if (value is null) return false;

        var comparison = value.Value.CompareTo(search.Value);
        return search.Comparison switch
        {
            PopulationDateComparison.Equal => comparison == 0,
            PopulationDateComparison.NotEqual => comparison != 0,
            PopulationDateComparison.GreaterThan => comparison > 0,
            PopulationDateComparison.LessThan => comparison < 0,
            PopulationDateComparison.GreaterThanOrEqual => comparison >= 0,
            PopulationDateComparison.LessThanOrEqual => comparison <= 0,
            _ => false
        };
    }

    private static CodeableConcept ToCodeableConcept(CodedValue value) =>
        new(value.System, value.Code, value.Display);

    private static CodeableConcept ToCodeableConcept(PopulationCode value)
    {
        var concept = new CodeableConcept { Text = value.Display };
        foreach (var coding in PopulationCodes.CodingsFor(value))
        {
            concept.Coding.Add(new Coding(coding.System!, coding.Code!, coding.Display));
        }

        return concept;
    }

    private static DataType? ToDataType(PopulationValue? value) => value switch
    {
        null => null,
        BooleanValue x => new FhirBoolean(x.Value),
        IntegerValue x => new Integer(x.Value),
        DecimalValue x => new FhirDecimal(x.Value),
        DateValue x => new FhirDateTime(x.Value.ToString("yyyy-MM-dd")),
        DateTimeValue x => new FhirDateTime(x.Value),
        TextValue x => new FhirString(x.Value),
        CodedValue x => ToCodeableConcept(x),
        QuantityValue x => new Quantity
        {
            Value = x.Value,
            Unit = x.Unit,
            System = x.System,
            Code = x.Code
        },
        _ => throw new InvalidOperationException($"Unsupported population value {value.GetType().Name}.")
    };
}
