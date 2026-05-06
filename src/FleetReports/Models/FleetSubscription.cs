namespace FleetReports.Models;

public record FleetSubscription
{
    public string ReportId { get; set; } = string.Empty;
    public HashSet<int> FleetMemberIds { get; set; } = [];
    public DateTime ExpiryTime { get; set; }
}
