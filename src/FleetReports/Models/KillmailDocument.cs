namespace FleetReports.Models;

public class KillmailDocument
{
    public int Id { get; set; }
    public string Hash { get; set; } = string.Empty;
    public DateTime KillmailTime { get; set; }
    public string SystemName { get; set; } = string.Empty;
    public int ShipTypeId { get; set; }
    public decimal TotalValue { get; set; }
    public int? VictimId { get; set; }
    public int? TopDamageId { get; set; }
    public int? FinalBlowId { get; set; }
}
