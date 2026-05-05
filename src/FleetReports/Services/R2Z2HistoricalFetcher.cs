using System.Net;
using FleetReports.Models;

namespace FleetReports.Services;

public class R2Z2HistoricalFetcher(IHttpClientFactory httpClientFactory, IKillmailCacheService cache) : IR2Z2HistoricalFetcher
{
    public async Task<IReadOnlyList<KillmailDocument>> FetchAsync(IEnumerable<int> fleetMemberIds, DateTime startTime, DateTime endTime)
    {
        var client = httpClientFactory.CreateClient("r2z2");
        var result = new List<KillmailDocument>();
        var memberSet = fleetMemberIds.ToHashSet();

        var seqResponse = await client.GetFromJsonAsync<R2Z2SequenceResponse>("sequence.json");
        var seq = seqResponse!.Sequence;

        while (true)
        {
            var response = await client.GetAsync($"{seq}.json");
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                break;
            }

            var entry = await response.Content.ReadFromJsonAsync<R2Z2KillmailEntry>();

            if (entry is null)
            {
                break;
            }

            if (entry.KillmailTime < startTime)
            {
                break;
            }

            if (entry.KillmailTime > endTime)
            {
                seq--;
                continue;
            }

            bool isMemberMatch = (entry.Victim.CharacterId.HasValue && memberSet.Contains(entry.Victim.CharacterId.Value))
                || entry.Attackers.Any(x => x.CharacterId.HasValue && memberSet.Contains(x.CharacterId.Value));

            if (isMemberMatch)
            {
                var doc = await cache.UpsertAsync(MapToEsiKillmail(entry), entry.Zkb.Hash, entry.Zkb.TotalValue);
                result.Add(doc);
            }

            seq--;
        }

        return result;
    }

    private static EsiKillmail MapToEsiKillmail(R2Z2KillmailEntry r2Z2Killmail)
    {
        return new EsiKillmail
        {
            KillmailId = r2Z2Killmail.KillmailId,
            KillmailTime = r2Z2Killmail.KillmailTime,
            SolarSystemId = r2Z2Killmail.SolarSystemId,
            Attackers = r2Z2Killmail.Attackers,
            Victim = r2Z2Killmail.Victim
        };
    }
}
