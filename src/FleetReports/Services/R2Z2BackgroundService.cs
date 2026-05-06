using System.Collections.Concurrent;
using System.Net;
using FleetReports.Models;
using LiteDB;

namespace FleetReports.Services;

public class R2Z2BackgroundService(
    IHttpClientFactory httpClientFactory,
    IKillmailCacheService killmailCache,
    LiteDatabase db,
    IReportUpdateNotifier notifier,
    TimeProvider timeProvider) : BackgroundService, IFleetSubscriptionRegistry
{
    private ConcurrentDictionary<string, FleetSubscription> _subscriptions = new();
    private SemaphoreSlim _wake = new SemaphoreSlim(0, 1); //signals loop to start
    public Func<Task> RateLimit = () => Task.Delay(63000);

    public void Register(FleetSubscription subscription)
    {
        _subscriptions.TryAdd(subscription.ReportId, subscription);
        if (_subscriptions.Count == 1)
        {
            _wake.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_subscriptions.IsEmpty)
            {
                await _wake.WaitAsync(stoppingToken);
            }

            var client = httpClientFactory.CreateClient("r2z2");
            var seqResponse = await client.GetFromJsonAsync<R2Z2SequenceResponse>("sequence.json");
            var seq = seqResponse!.Sequence;

            await PollloopAsync(client, seq, stoppingToken);
        }
    }

    private async Task PollloopAsync(HttpClient client, int seq, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            PurgeExpiredSubscriptions();
            {

                if (_subscriptions.IsEmpty)
                {
                    return;
                }

                var response = await client.GetAsync($"{seq}.json", ct);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    await RateLimit();
                    continue; // retry same seq
                }

                var entry = await response.Content.ReadFromJsonAsync<R2Z2KillmailEntry>(ct);
                if (entry is not null)
                {
                    await ProcessKillmailAsync(entry);
                }

                await Task.Delay(100, ct);
                seq++;
            }
        }
    }

    public void PurgeExpiredSubscriptions()
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var key in _subscriptions.Keys)
            if (_subscriptions.TryGetValue(key, out var sub) && now > sub.ExpiryTime)
                _subscriptions.TryRemove(key, out _);
    }

    public async Task ProcessKillmailAsync(R2Z2KillmailEntry entry)
    {
        foreach (var sub in _subscriptions.Values)
        {
            bool isVictim = entry.Victim.CharacterId.HasValue
                && sub.FleetMemberIds.Contains(entry.Victim.CharacterId.Value);
            bool isAttacker = entry.Attackers.Any(a =>
                a.CharacterId.HasValue && sub.FleetMemberIds.Contains(a.CharacterId.Value));

            if (!isVictim && !isAttacker)
            {
                continue;
            }

            var doc = await killmailCache.UpsertAsync(MapToEsiKillmail(entry), entry.Zkb.Hash, entry.Zkb.TotalValue);

            UpdateReport(sub.ReportId, doc, isVictim, isAttacker, sub.FleetMemberIds);
        }
    }

    private void UpdateReport(string reportId, KillmailDocument doc, bool isVictim, bool isAttacker, HashSet<int> fleetMemberIds)
    {
        var reports = db.GetCollection<ReportDocument>("reports");
        var report = reports.FindById(reportId);
        if (report is null)
        {
            return;
        }

        if (isVictim && !report.LossIds.Contains(doc.Id))
        {
            report.LossIds = [..report.LossIds, doc.Id];
        }

        if (isAttacker && !report.KillIds.Contains(doc.Id))
        {
            report.KillIds = [.. report.KillIds, doc.Id];
        }

        report.TotalKills = report.KillIds.Length;
        report.TotalLosses = report.LossIds.Length;
        report.IskDestroyed += isAttacker ? doc.TotalValue : 0;
        report.IskLost += isVictim ? doc.TotalValue : 0;

        reports.Update(report);
        notifier.Notify(report.Id);
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
