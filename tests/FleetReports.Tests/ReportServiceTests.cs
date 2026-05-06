using FleetReports.Models;
using FleetReports.Services;
using LiteDB;
using Moq;

namespace FleetReports.Tests;

public class ReportServiceTests
{
    private static readonly DateTime FixedNow = new(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly string[] TwoMembers = ["Alice", "Bob"];
    private static readonly DateTime PastEnd = FixedNow.AddHours(-1);
    private static readonly DateTime FutureEnd = FixedNow.AddHours(1);

    private record Ctx(
        ReportService Svc,
        Mock<IFleetSubscriptionRegistry> Registry,
        LiteDatabase Db,
        Mock<IR2Z2HistoricalFetcher> R2Z2,
        Mock<IZkillCharacterFetcher> Zkill);

    private static Ctx Build(
        IReadOnlyList<KillmailDocument> docs,
        Dictionary<string, int>? members = null)
    {
        members ??= new Dictionary<string, int> { ["Alice"] = 100, ["Bob"] = 200 };

        var characters = new Mock<ICharacterService>();
        characters.Setup(x => x.ResolveNamesAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(members);

        var r2z2 = new Mock<IR2Z2HistoricalFetcher>();
        r2z2.Setup(x => x.FetchAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(docs);

        var zkill = new Mock<IZkillCharacterFetcher>();
        zkill.Setup(x => x.FetchAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .ReturnsAsync(docs);

        var db = new LiteDatabase(":memory:");
        var registry = new Mock<IFleetSubscriptionRegistry>();

        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(FixedNow));

        var svc = new ReportService(
            characters.Object, r2z2.Object, zkill.Object,
            db, registry.Object, timeProvider.Object);

        return new Ctx(svc, registry, db, r2z2, zkill);
    }

    // members: Alice=100, Bob=200, enemy=999
    private static KillmailDocument Kill(int id, int? victimId, int[] attackerIds,
        decimal value = 1_000_000m, int? topDamageId = null) => new()
    {
        Id = id,
        Hash = "h",
        KillmailTime = FixedNow.AddHours(-1),
        SystemName = "Jita",
        ShipTypeId = 587,
        TotalValue = value,
        VictimId = victimId,
        AttackerIds = attackerIds,
        TopDamageId = topDamageId ?? (attackerIds.Length > 0 ? attackerIds[0] : null),
        FinalBlowId = attackerIds.Length > 0 ? attackerIds[^1] : null
    };

    private static ReportDocument GetReport(LiteDatabase db) =>
        db.GetCollection<ReportDocument>("reports").FindAll().Single();

    [Fact]
    public async Task FleetMemberAsAttacker_AppearsInKillIdsOnly()
    {
        var doc = Kill(1, victimId: 999, attackerIds: [100]);
        var ctx = Build([doc]);

        await ctx.Svc.CreateReportAsync(TwoMembers, FixedNow.AddHours(-2), PastEnd);

        var report = GetReport(ctx.Db);
        Assert.Contains(1, report.KillIds);
        Assert.DoesNotContain(1, report.LossIds);
    }

    [Fact]
    public async Task FleetMemberAsVictim_AppearsInLossIdsOnly()
    {
        var doc = Kill(1, victimId: 100, attackerIds: [999]);
        var ctx = Build([doc]);

        await ctx.Svc.CreateReportAsync(TwoMembers, FixedNow.AddHours(-2), PastEnd);

        var report = GetReport(ctx.Db);
        Assert.Contains(1, report.LossIds);
        Assert.DoesNotContain(1, report.KillIds);
    }

    [Fact]
    public async Task FleetVsFleet_AppearsInBothLists()
    {
        // Alice (100) is victim, Bob (200) is attacker
        var doc = Kill(1, victimId: 100, attackerIds: [200]);
        var ctx = Build([doc]);

        await ctx.Svc.CreateReportAsync(TwoMembers, FixedNow.AddHours(-2), PastEnd);

        var report = GetReport(ctx.Db);
        Assert.Contains(1, report.KillIds);
        Assert.Contains(1, report.LossIds);
    }

    [Fact]
    public async Task IskTotals_SumCorrectly()
    {
        var kill = Kill(1, victimId: 999, attackerIds: [100], value: 5_000_000m);
        var loss = Kill(2, victimId: 100, attackerIds: [999], value: 3_000_000m);
        var ctx = Build([kill, loss], members: new Dictionary<string, int> { ["Alice"] = 100 });

        await ctx.Svc.CreateReportAsync(["Alice"], FixedNow.AddHours(-2), PastEnd);

        var report = GetReport(ctx.Db);
        Assert.Equal(5_000_000m, report.IskDestroyed);
        Assert.Equal(3_000_000m, report.IskLost);
    }

    [Fact]
    public async Task EndTimeInPast_DoesNotRegisterSubscription()
    {
        var ctx = Build([]);

        await ctx.Svc.CreateReportAsync(TwoMembers, FixedNow.AddHours(-2), PastEnd);

        ctx.Registry.Verify(x => x.Register(It.IsAny<FleetSubscription>()), Times.Never);
    }

    [Fact]
    public async Task EndTimeInFuture_RegistersSubscriptionWithCorrectMembers()
    {
        var ctx = Build([]);

        await ctx.Svc.CreateReportAsync(TwoMembers, FixedNow.AddHours(-1), FutureEnd);

        ctx.Registry.Verify(x => x.Register(It.Is<FleetSubscription>(s =>
            s.FleetMemberIds.Contains(100) &&
            s.FleetMemberIds.Contains(200) &&
            s.ExpiryTime == FutureEnd)), Times.Once);
    }

    [Fact]
    public async Task EndTimeWithin24h_UsesR2Z2Fetcher()
    {
        var endTime = FixedNow.AddHours(-20); // within 24h cutoff
        var ctx = Build([]);

        await ctx.Svc.CreateReportAsync(TwoMembers, FixedNow.AddHours(-21), endTime);

        ctx.R2Z2.Verify(x => x.FetchAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
        ctx.Zkill.Verify(x => x.FetchAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task EndTimeOlderThan24h_UsesZkillFetcher()
    {
        var endTime = FixedNow.AddHours(-25); // older than 24h
        var ctx = Build([]);

        await ctx.Svc.CreateReportAsync(TwoMembers, FixedNow.AddHours(-26), endTime);

        ctx.Zkill.Verify(x => x.FetchAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Once);
        ctx.R2Z2.Verify(x => x.FetchAsync(It.IsAny<IEnumerable<int>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()), Times.Never);
    }

    [Fact]
    public async Task TopDamageDealerId_IsFleetMemberWithMostTopDamageAppearances()
    {
        // Alice (100) top damage on 2 kills, Bob (200) on 1 — Alice should win
        var k1 = Kill(1, victimId: 999, attackerIds: [100], topDamageId: 100);
        var k2 = Kill(2, victimId: 999, attackerIds: [100], topDamageId: 100);
        var k3 = Kill(3, victimId: 999, attackerIds: [200], topDamageId: 200);
        var ctx = Build([k1, k2, k3]);

        await ctx.Svc.CreateReportAsync(TwoMembers, FixedNow.AddHours(-2), PastEnd);

        var report = GetReport(ctx.Db);
        Assert.Equal(100, report.TopDamageDealerId);
    }
}
