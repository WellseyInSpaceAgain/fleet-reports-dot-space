namespace FleetReports.Models;

public class ReportDocument
{
    public string Id { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string[] FleetMemberNames { get; set; } = [];
    public int[] FleetMemberIds { get; set; } = [];
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int[] KillIds { get; set; } = [];
    public int[] LossIds { get; set; } = [];
    public int TotalKills { get; set; }
    public int TotalLosses { get; set; }
    public decimal IskDestroyed { get; set; }
    public decimal IskLost { get; set; }
    public int? TopDamageDealerId { get; set; }
}
