using FleetReports.Models;

namespace FleetReports.Services;

public interface IZkillCharacterFetcher
{
    Task<IReadOnlyList<KillmailDocument>> FetchAsync(IEnumerable<int> fleetMemberIds, DateTime startTime, DateTime endTime);
}
