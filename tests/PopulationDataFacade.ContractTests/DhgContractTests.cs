using System.Text.Json;
using PopulationDataFacade.Infrastructure.Dhg;
using Xunit;

namespace PopulationDataFacade.ContractTests;

public sealed class DhgContractTests
{
    [Fact]
    public async Task Documented_resource_areas_deserialize_with_exact_wire_names()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "dhg-record-all-resources.json");
        await using var stream = File.OpenRead(path);
        var record = await JsonSerializer.DeserializeAsync<DhgMaternityRecord>(
            stream,
            DhgJson.Options,
            TestContext.Current.CancellationToken);

        Assert.NotNull(record);
        Assert.NotNull(record.Metadata);
        Assert.NotNull(record.AntenatalAppointments);
        var fetus = Assert.Single(Assert.Single(record.AntenatalAppointments).FetusesVitalSigns!);
        Assert.Equal(1, fetus.FetusId);
        Assert.Equal(146, fetus.FetalHeartRate);
        Assert.Equal("1", fetus.FetalPresentationLie?.Code);
        Assert.True(fetus.MotherFeelsBabyMovements);
        Assert.Equal("Normale funn", fetus.Note);
        Assert.NotNull(record.BirthStatus);
        var birthStatus = Assert.Single(record.BirthStatus.BirthStatus!);
        Assert.Equal(1, birthStatus.FetusId);
        Assert.Equal("1", birthStatus.Status?.Code);
        Assert.Equal("Født levende", birthStatus.Status?.Display);
        Assert.Equal("VOLVEN_8522", birthStatus.Status?.CodeSystem);
        Assert.Equal(
            DateTimeOffset.Parse("2026-05-01T08:00:00+02:00"),
            birthStatus.DateTime);
        Assert.NotNull(record.ClinicalTests);
        Assert.NotNull(record.CurrentPregnancy);
        Assert.NotNull(record.GeneticDisorders);
        Assert.NotNull(record.LifestyleFactors);
        Assert.NotNull(record.MedicalConditions);
        Assert.NotNull(record.Medication);
        Assert.NotNull(record.Mother);
        Assert.Equal("Mamma Ku", record.Mother.Name);
        Assert.Equal("Gate Gatesen 12", record.Mother.Address);
        Assert.Equal("0500", record.Mother.PostNumber);
        Assert.Equal("Oslo", record.Mother.PostName);
        Assert.True(record.Mother.EmployedLastSixMonths);
        Assert.Equal(100, record.Mother.EmploymentPercentage);
        Assert.Equal("Snekker", record.Mother.OccupationAndIndustry);
        Assert.Equal("NOB", record.Mother.Language?.Code);
        Assert.Equal("NO", record.Mother.CountryOfBirth?.Code);
        Assert.False(record.Mother.NeedsLanguageInterpreter);
        Assert.False(record.Mother.CohabitingCoparent);
        Assert.Equal("Bor med storfamilie", record.Mother.CohabitingCoparentNote);
        Assert.NotNull(record.PointsOfContact);
        Assert.Equal("Ola Fastlege", record.PointsOfContact.GeneralPractitioner?.Name);
        Assert.Equal("Test legekontor", record.PointsOfContact.GeneralPractitioner?.OrganizationName);
        Assert.Equal("994598759", record.PointsOfContact.GeneralPractitioner?.OrganizationId);
        Assert.Equal("1234567", record.PointsOfContact.GeneralPractitioner?.HprNumber);
        Assert.Equal("Kari Jordmor", record.PointsOfContact.Midwife?.Name);
        Assert.Equal("Sentrum jordmortjeneste", record.PointsOfContact.Midwife?.OrganizationName);
        Assert.Equal("7654321", record.PointsOfContact.Midwife?.HprNumber);
        Assert.Equal("Testsykehus", record.PointsOfContact.BirthInstitute);
        Assert.Equal("Sentrum helsestasjon", record.PointsOfContact.MaternityHealthcareCentre);
        Assert.NotNull(record.PreviousPregnancies);
        Assert.NotNull(record.RhesusDNegative);
        Assert.NotNull(record.SymphysisFundalHeights);
        Assert.NotNull(record.VitalMeasurementsBeforePregnancy);
        Assert.Equal(22.1m, record.VitalMeasurementsBeforePregnancy.BMI);
        Assert.True(record.AdditionalProperties?.ContainsKey("futureRootProperty"));
    }

    [Fact]
    public void Json_contract_is_case_sensitive_and_forward_compatible()
    {
        const string json = """
            { "bMI": 21.5, "bmi": 99, "newClinicalProperty": "future" }
            """;

        var value = JsonSerializer.Deserialize<DhgVitalMeasurementsBeforePregnancy>(json, DhgJson.Options);

        Assert.NotNull(value);
        Assert.Equal(21.5m, value.BMI);
        Assert.Equal(2, value.AdditionalProperties?.Count);
        Assert.True(value.AdditionalProperties?.ContainsKey("bmi"));
        Assert.True(value.AdditionalProperties?.ContainsKey("newClinicalProperty"));
    }

    [Fact]
    public void Nullable_booleans_keep_false_and_null_distinct()
    {
        var value = JsonSerializer.Deserialize<DhgClinicalTests>("{\"hbv\":false,\"hiv\":null}", DhgJson.Options);

        Assert.NotNull(value);
        Assert.False(value.Hbv);
        Assert.Null(value.Hiv);
    }
}
