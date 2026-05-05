using FleetReports.Models;

namespace FleetReports.Services;

public interface IKillmailCacheService
{
    Task<KillmailDocument?> GetAsync(int killmailId);
    Task<KillmailDocument> UpsertAsync(EsiKillmail killmail, string hash, decimal totalValue);
}
