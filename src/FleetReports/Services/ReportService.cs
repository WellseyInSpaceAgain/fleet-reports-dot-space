using FleetReports.Models;
using LiteDB;
using NanoidDotNet;

namespace FleetReports.Services;

public class ReportService(
    ICharacterService characterService,
    IR2Z2HistoricalFetcher r2Z2HistoricalFetcher,
    IZkillCharacterFetcher zkillCharacterFetcher,
    LiteDatabase db,
    IFleetSubscriptionRegistry fleetSubscriptionRegistry,
    TimeProvider timeProvider) : IReportService
{
    private readonly ILiteCollection<ReportDocument> _reports = db.GetCollection<ReportDocument>("reports");
    
    public async Task<string> CreateReportAsync(string[] names, DateTime startTime, DateTime endTime)
    {
        var nameMap = await characterService.ResolveNamesAsync(names);
        var memberIds = nameMap.Values.Distinct().ToArray();
        var memberSet = memberIds.ToHashSet();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var cutoff = now.AddHours(-24);

        IReadOnlyList<KillmailDocument> documents = endTime > cutoff
            ? await r2Z2HistoricalFetcher.FetchAsync(memberIds, startTime, endTime)
            : await zkillCharacterFetcher.FetchAsync(memberIds, startTime, endTime);

        var killIds = new List<int>();
        var lossIds = new List<int>();

        foreach (var doc in documents)
        {
            var isVictim = doc.VictimId.HasValue && memberSet.Contains(doc.VictimId.Value);
            var isAttacker = doc.AttackerIds.Any(x => memberSet.Contains(x));

            if (isVictim)
            {
                lossIds.Add(doc.Id);
            }

            if (!isVictim || isAttacker)
            {
                killIds.Add(doc.Id);
            }
        }

        var killSet = killIds.ToHashSet();
        var lossSet = lossIds.ToHashSet();

        var killDocs = documents.Where(d => killSet.Contains(d.Id)).ToList();
        var lossDocs = documents.Where(d => lossSet.Contains(d.Id)).ToList();

        var topDamageDealerId = killDocs
            .Where(d => d.TopDamageId.HasValue && memberSet.Contains(d.TopDamageId.Value))
            .GroupBy(d => d.TopDamageId!.Value)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key;

        var id = await Nanoid.GenerateAsync(size: 10);

        var report = new ReportDocument
        {
            Id = id,
            CreatedAt = now,
            FleetMemberNames = names,
            FleetMemberIds = memberIds,
            StartTime = startTime,
            EndTime = endTime,
            KillIds = killIds.ToArray(),
            LossIds = lossIds.ToArray(),
            TotalKills = killIds.Count(),
            TotalLosses = lossIds.Count(),
            IskDestroyed = killDocs.Sum(d => d.TotalValue),
            IskLost = lossDocs.Sum(d => d.TotalValue),
            TopDamageDealerId = topDamageDealerId
        };

        _reports.Insert(report);

        if (endTime > now)
        {
            fleetSubscriptionRegistry.Register(new FleetSubscription
            {
                ReportId = id,
                FleetMemberIds = memberSet,
                ExpiryTime = endTime
            });
        }

        return id;
    }
}
