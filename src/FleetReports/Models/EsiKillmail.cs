using System.Text.Json.Serialization;

namespace FleetReports.Models;

public class EsiKillmail
{
    [JsonPropertyName("killmail_id")]
    public int KillmailId { get; set; }

    [JsonPropertyName("killmail_time")]
    public DateTime KillmailTime { get; set; }

    [JsonPropertyName("solar_system_id")]
    public int SolarSystemId { get; set; }

    [JsonPropertyName("attackers")]
    public List<EsiKillmailAttacker> Attackers { get; set; } = [];

    [JsonPropertyName("victim")]
    public EsiKillmailVictim Victim { get; set; } = new();
}
