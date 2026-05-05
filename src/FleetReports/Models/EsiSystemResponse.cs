using System.Text.Json.Serialization;

namespace FleetReports.Models;

public class EsiSystemResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
