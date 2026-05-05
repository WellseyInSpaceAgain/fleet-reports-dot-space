using FleetReports.Models;

namespace FleetReports.Services;

public interface IR2Z2HistoricalFetcher
{
    Task<IReadOnlyList<KillmailDocument>> FetchAsync(IEnumerable<int> fleetMemberIds, DateTime startTime, DateTime endTime);
}
