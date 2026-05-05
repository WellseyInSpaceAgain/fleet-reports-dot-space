using System.Text.Json.Serialization;

namespace FleetReports.Models;

public class R2Z2KillmailEntry
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

    [JsonPropertyName("zkb")]
    public R2Z2ZkbBlock Zkb { get; set; } = new();
}

public class R2Z2ZkbBlock
{
    [JsonPropertyName("hash")]
    public string Hash { get; set; } = string.Empty;

    [JsonPropertyName("totalValue")]
    public decimal TotalValue { get; set; }
}

public class R2Z2SequenceResponse
{
    [JsonPropertyName("sequence")]
    public int Sequence { get; set; }
}
