using System.Text.Json.Serialization;

namespace FleetReports.Services;

public record UniverseIdsResponse(
        [property: JsonPropertyName("characters")] List<CharacterEntry> Characters);
