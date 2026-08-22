using Hl7.Fhir.Model;

namespace PopulationDataFacade.Core;

public interface IFhirPopulationMapper
{
    Patient MapPatient(PopulationPatient patient);
    IReadOnlyList<Observation> MapObservations(PopulationSnapshot snapshot, PopulationCode? filter = null);
    IReadOnlyList<Encounter> MapEncounters(PopulationSnapshot snapshot);
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

    public IReadOnlyList<Observation> MapObservations(PopulationSnapshot snapshot, PopulationCode? filter = null)
    {
        return snapshot.Observations
            .Where(x => filter is null || PopulationCodes.Matches(x.Code, filter))
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
            Period = new Period(new FhirDateTime(x.Date.ToString("yyyy-MM-dd")), new FhirDateTime(x.Date.ToString("yyyy-MM-dd")))
        })
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
                        Hl7.Fhir.Model.CapabilityStatement.TypeRestfulInteraction.SearchType)
                ]
            }
        ]
    };

    private static Observation MapObservation(string patientId, PopulationObservation source)
    {
        var profiles = ObservationProfiles(source);
        var observation = new Observation
        {
            Id = source.Id,
            Meta = Meta(source.LastUpdated, profiles),
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

        if (!string.IsNullOrWhiteSpace(source.Note))
        {
            observation.Note = [new Annotation { Text = source.Note }];
        }

        return observation;
    }

    private static CapabilityStatement.ResourceComponent ResourceCapability(
        ResourceType resourceType,
        bool includePatientIdentifier,
        params CapabilityStatement.TypeRestfulInteraction[] interactions) => new()
    {
        Type = resourceType.ToString(),
        SupportedProfile = resourceType is ResourceType.Observation
            ?
            [
                PopulationProfiles.NorwegianVitalSignsBodyWeight
            ]
            : [],
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
                }
            ]
            : resourceType is ResourceType.Encounter
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

    private static IReadOnlyList<string> ObservationProfiles(PopulationObservation source)
    {
        if (!string.Equals(source.Category, "vital-signs", StringComparison.Ordinal) ||
            source.Effective is null)
        {
            return [];
        }

        if (source.Code == PopulationCodes.MotherWeight &&
            source.Value is QuantityValue { System: PopulationCodes.Ucum, Code: "kg" })
        {
            return [PopulationProfiles.NorwegianVitalSignsBodyWeight];
        }

        return [];
    }

    private static Meta? Meta(DateTimeOffset? lastUpdated, IReadOnlyList<string>? profiles = null)
    {
        if (lastUpdated is null && (profiles is null || profiles.Count == 0))
            return null;

        var meta = new Meta { LastUpdated = lastUpdated };
        if (profiles is { Count: > 0 })
            meta.Profile = profiles.ToList();

        return meta;
    }

    private static CodeableConcept ToCodeableConcept(CodedValue value) =>
        new(value.System, value.Code, value.Display);

    private static CodeableConcept ToCodeableConcept(PopulationCode value) => new()
    {
        Coding = PopulationCodes.CodingsFor(value)
            .Select(coding => new Coding(coding.System, coding.Code, coding.Display))
            .ToList(),
        Text = value.Display
    };

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
