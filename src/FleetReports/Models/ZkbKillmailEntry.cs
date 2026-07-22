using System.Text.Json.Serialization;

namespace FleetReports.Models;

public record ZkbKillmailEntry
{
    [JsonPropertyName("killmail_id")]
    public int KillmailId { get; set; }

    [JsonPropertyName("zkb")]
    public ZkbInfo Zkb { get; set; } = new();
}

public record ZkbInfo
{
    [JsonPropertyName("hash")]
    public string Hash { get; set; } = string.Empty;
    
    [JsonPropertyName("totalValue")]
    public decimal TotalValue { get; set; }
}
