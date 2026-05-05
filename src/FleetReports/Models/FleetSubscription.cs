namespace FleetReports.Models;

public class FleetSubscription
{
    public string ReportId { get; set; } = string.Empty;
    public HashSet<int> FleetMemberIds { get; set; } = [];
    public DateTime ExpiryTime { get; set; }
}
