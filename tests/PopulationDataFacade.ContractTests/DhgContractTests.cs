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
        var record = await JsonSerializer.DeserializeAsync<DhgMaternityRecord>(stream, DhgJson.Options);

        Assert.NotNull(record);
        Assert.NotNull(record.Metadata);
        Assert.NotNull(record.AntenatalAppointments);
        Assert.NotNull(record.BirthStatus);
        Assert.NotNull(record.ClinicalTests);
        Assert.NotNull(record.CurrentPregnancy);
        Assert.NotNull(record.GeneticDisorders);
        Assert.NotNull(record.LifestyleFactors);
        Assert.NotNull(record.MedicalConditions);
        Assert.NotNull(record.Medication);
        Assert.NotNull(record.Mother);
        Assert.NotNull(record.PointsOfContact);
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
