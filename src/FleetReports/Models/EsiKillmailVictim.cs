using System.Text.Json.Serialization;

namespace FleetReports.Models;

public class EsiKillmailVictim
{
    [JsonPropertyName("character_id")]
    public int? CharacterId { get; set; }

    [JsonPropertyName("ship_type_id")]
    public int ShipTypeId { get; set; }
}
