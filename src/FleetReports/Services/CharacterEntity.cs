using System.Text.Json.Serialization;

namespace FleetReports.Services;

public record CharacterEntry(
        [property: JsonPropertyName("id")] int Id,
        [property: JsonPropertyName("name")] string Name);
