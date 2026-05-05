using System.Text.Json.Serialization;

namespace FleetReports.Models;

public class ZkbKillmailEntry
{
    [JsonPropertyName("killmail_id")]
    public int KillmailId { get; set; }

    [JsonPropertyName("hash")]
    public string Hash { get; set; } = string.Empty;

    [JsonPropertyName("zkb")]
    public ZkbInfo Zkb { get; set; } = new();
}

public class ZkbInfo
{
    [JsonPropertyName("totalValue")]
    public decimal TotalValue { get; set; }
}
