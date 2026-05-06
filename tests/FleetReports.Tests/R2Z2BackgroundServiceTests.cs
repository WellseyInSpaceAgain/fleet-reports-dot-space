using FleetReports.Models;
using FleetReports.Services;
using LiteDB;
using Moq;

namespace FleetReports.Tests;

public class R2Z2BackgroundServiceTests
{
    private static readonly DateTime FixedNow = new(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);

    private record Ctx(
        R2Z2BackgroundService Svc,
        Mock<IKillmailCacheService> Cache,
        Mock<IReportUpdateNotifier> Notifier,
        LiteDatabase Db,
        Mock<TimeProvider> Time);

    private static Ctx Build()
    {
        var cache = new Mock<IKillmailCacheService>();
        var notifier = new Mock<IReportUpdateNotifier>();
        var db = new LiteDatabase(":memory:");
        var httpFactory = new Mock<IHttpClientFactory>();
        var time = new Mock<TimeProvider>();
        time.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(FixedNow));

        var svc = new R2Z2BackgroundService(
            httpFactory.Object, cache.Object, db, notifier.Object, time.Object);

        return new Ctx(svc, cache, notifier, db, time);
    }

    private static ReportDocument InsertReport(LiteDatabase db, string reportId, int[] memberIds)
    {
        var report = new ReportDocument
        {
            Id = reportId,
            CreatedAt = FixedNow,
            FleetMemberNames = [],
            FleetMemberIds = memberIds,
            StartTime = FixedNow.AddHours(-1),
            EndTime = FixedNow.AddHours(1),
            KillIds = [],
            LossIds = [],
            TotalKills = 0,
            TotalLosses = 0,
            IskDestroyed = 0,
            IskLost = 0
        };
        db.GetCollection<ReportDocument>("reports").Insert(report);
        return report;
    }

    private static R2Z2KillmailEntry MakeEntry(int killmailId, int? victimId, int[] attackerIds, decimal value = 1_000_000m) =>
        new()
        {
            KillmailId = killmailId,
            KillmailTime = FixedNow.AddMinutes(-5),
            SolarSystemId = 30000142,
            Victim = new EsiKillmailVictim { CharacterId = victimId, ShipTypeId = 587 },
            Attackers = attackerIds.Select((id, i) => new EsiKillmailAttacker
            {
                CharacterId = id,
                DamageDone = 100,
                FinalBlow = i == attackerIds.Length - 1
            }).ToList(),
            Zkb = new R2Z2ZkbBlock { Hash = "testhash", TotalValue = value }
        };

    private static KillmailDocument MakeDoc(int id) => new()
    {
        Id = id,
        Hash = "testhash",
        KillmailTime = FixedNow.AddMinutes(-5),
        SystemName = "Jita",
        ShipTypeId = 587,
        TotalValue = 1_000_000m,
        AttackerIds = [],
        VictimId = null
    };

    [Fact]
    public async Task NoFleetMemberMatch_NothingWritten_NothingNotified()
    {
        var ctx = Build();
        InsertReport(ctx.Db, "r1", [100]);
        ctx.Svc.Register(new FleetSubscription
        {
            ReportId = "r1",
            FleetMemberIds = [100],
            ExpiryTime = FixedNow.AddHours(1)
        });

        // killmail has attacker 999 — not a fleet member
        var entry = MakeEntry(1, victimId: null, attackerIds: [999]);

        await ctx.Svc.ProcessKillmailAsync(entry);

        ctx.Cache.Verify(x => x.UpsertAsync(It.IsAny<EsiKillmail>(), It.IsAny<string>(), It.IsAny<decimal>()), Times.Never);
        ctx.Notifier.Verify(x => x.Notify(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task SingleKillmail_MatchesTwoSubscriptions_BothNotified()
    {
        var ctx = Build();
        InsertReport(ctx.Db, "r1", [100]);
        InsertReport(ctx.Db, "r2", [100]);

        var doc = MakeDoc(1);
        ctx.Cache.Setup(x => x.UpsertAsync(It.IsAny<EsiKillmail>(), It.IsAny<string>(), It.IsAny<decimal>()))
            .ReturnsAsync(doc);

        ctx.Svc.Register(new FleetSubscription
        {
            ReportId = "r1",
            FleetMemberIds = [100],
            ExpiryTime = FixedNow.AddHours(1)
        });
        ctx.Svc.Register(new FleetSubscription
        {
            ReportId = "r2",
            FleetMemberIds = [100],
            ExpiryTime = FixedNow.AddHours(1)
        });

        var entry = MakeEntry(1, victimId: null, attackerIds: [100]);

        await ctx.Svc.ProcessKillmailAsync(entry);

        ctx.Notifier.Verify(x => x.Notify("r1"), Times.Once);
        ctx.Notifier.Verify(x => x.Notify("r2"), Times.Once);
    }

    [Fact]
    public async Task ExpiredSubscription_PurgedBeforeFanOut_NotNotified()
    {
        var ctx = Build();
        InsertReport(ctx.Db, "r1", [100]);

        ctx.Svc.Register(new FleetSubscription
        {
            ReportId = "r1",
            FleetMemberIds = [100],
            ExpiryTime = FixedNow.AddSeconds(-1) // already expired
        });

        ctx.Svc.PurgeExpiredSubscriptions();

        var entry = MakeEntry(1, victimId: null, attackerIds: [100]);

        await ctx.Svc.ProcessKillmailAsync(entry);

        ctx.Cache.Verify(x => x.UpsertAsync(It.IsAny<EsiKillmail>(), It.IsAny<string>(), It.IsAny<decimal>()), Times.Never);
        ctx.Notifier.Verify(x => x.Notify(It.IsAny<string>()), Times.Never);
    }
}
