using System.Text.Json.Serialization;

namespace FleetReports.Models;

public class EsiKillmailAttacker
{
    [JsonPropertyName("character_id")]
    public int? CharacterId { get; set; }

    [JsonPropertyName("damage_done")]
    public int DamageDone { get; set; }

    [JsonPropertyName("final_blow")]
    public bool FinalBlow { get; set; }
}
