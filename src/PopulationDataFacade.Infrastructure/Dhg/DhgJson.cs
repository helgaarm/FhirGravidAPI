using System.Text.Json;
using System.Text.Json.Serialization;

namespace PopulationDataFacade.Infrastructure.Dhg;

public static class DhgJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
        NumberHandling = JsonNumberHandling.Strict
    };
}
