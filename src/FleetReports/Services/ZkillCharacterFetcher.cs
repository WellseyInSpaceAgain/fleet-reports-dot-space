using FleetReports.Models;

namespace FleetReports.Services;

public class ZkillCharacterFetcher(IHttpClientFactory httpClientFactory, IKillmailCacheService cache, IEsiService esi) : IZkillCharacterFetcher
{
    public Func<Task> RateLimit = () => Task.Delay(3000);
    public async Task<IReadOnlyList<KillmailDocument>> FetchAsync(IEnumerable<int> fleetMemberIds, DateTime startTime, DateTime endTime)
    {
        var allEntries = new Dictionary<int, ZkbKillmailEntry>();

        foreach (var memberId in fleetMemberIds)
        {
            var entries = await FetchForCharacterAsync(memberId, startTime, endTime);
            foreach (var entry in entries)
            {
                allEntries.TryAdd(entry.KillmailId, entry);
            }
        }

        var result = new List<KillmailDocument>();

        foreach (var entry in allEntries.Values)
        {
            var document = await ResolveEntryAsync(entry, startTime, endTime);
            if (document is not null)
            {
                result.Add(document);
            }
        }

        return result;
    }

    private async Task<List<ZkbKillmailEntry>> FetchForCharacterAsync(int characterId, DateTime startTime, DateTime endTime)
    {
        var entries = new List<ZkbKillmailEntry>();
        var client = httpClientFactory.CreateClient("zkb");

        foreach (var (year, month) in GetMonthsInRange(startTime, endTime))
        {
            var page = 1;
            while (true)
            {
                await RateLimit();
                var url = $"api/characterID/{characterId}/year/{year}/month/{month}/page/{page}/";
                var pageEntries = await client.GetFromJsonAsync<List<ZkbKillmailEntry>>(url);

                if (pageEntries is null || pageEntries.Count == 0)
                {
                    break;
                }

                entries.AddRange(pageEntries);
                page++;
            }
        }
        return entries;
    }

    private async Task<KillmailDocument?> ResolveEntryAsync(ZkbKillmailEntry entry, DateTime startTime, DateTime endTime)
    {
        var cached = await cache.GetAsync(entry.KillmailId);
        if (cached is not null && cached.Hash == entry.Zkb.Hash)
        {
            return cached.KillmailTime >= startTime && cached.KillmailTime <= endTime
                ? cached : null;
        }

        var killmail = await esi.GetAsync<EsiKillmail>($"killmails/{entry.KillmailId}/{entry.Zkb.Hash}/");
        if (killmail is null)
        {
            return null;
        }

        if (killmail.KillmailTime >= startTime && killmail.KillmailTime <= endTime)
        {
            var document = await cache.UpsertAsync(killmail, entry.Zkb.Hash, entry.Zkb.TotalValue);
            return document;
        }
        else
        {
            return null;
        }
    }

    private static IEnumerable<(int year, int month)> GetMonthsInRange(DateTime start, DateTime end)
    {
        var current = new DateTime(start.Year, start.Month, 1);
        var last = new DateTime(end.Year, end.Month, 1);
        while (current <= last)
        {
            yield return (current.Year, current.Month);
            current = current.AddMonths(1);
        }
    }
}
