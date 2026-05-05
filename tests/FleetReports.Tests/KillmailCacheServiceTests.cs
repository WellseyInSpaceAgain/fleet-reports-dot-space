using FleetReports.Models;
using FleetReports.Services;
using LiteDB;
using Moq;

namespace FleetReports.Tests;

public class KillmailCacheServiceTests
{
    private static KillmailCacheService BuildService()
    {
        var db = new LiteDatabase(":memory:");
        var systemNames = new Mock<ISystemNameCacheService>();
        systemNames.Setup(x => x.GetOrResolveAsync(It.IsAny<int>()))
            .ReturnsAsync("Jita");
        return new KillmailCacheService(db, systemNames.Object);
    }

    private static EsiKillmail MakeKillmail(params (int? charId, int damage, bool finalBlow)[] attackers) =>
        new()
        {
            KillmailId = 1,
            KillmailTime = DateTime.UtcNow,
            SolarSystemId = 30000142,
            Victim = new EsiKillmailVictim { CharacterId = 999, ShipTypeId = 587 },
            Attackers = attackers.Select(a => new EsiKillmailAttacker
            {
                CharacterId = a.charId,
                DamageDone = a.damage,
                FinalBlow = a.finalBlow
            }).ToList()
        };

    [Fact]
    public async Task UpsertAsync_SetsTopDamageId_ToAttackerWithHighestDamage()
    {
        var svc = BuildService();
        var killmail = MakeKillmail((111, 500, false), (222, 1000, true));

        var doc = await svc.UpsertAsync(killmail, "hash1", 1_000_000m);

        Assert.Equal(222, doc.TopDamageId);
    }

    [Fact]
    public async Task UpsertAsync_SetsFinalBlowId_ToAttackerWithFinalBlowTrue()
    {
        var svc = BuildService();
        var killmail = MakeKillmail((111, 1000, false), (222, 500, true));

        var doc = await svc.UpsertAsync(killmail, "hash1", 1_000_000m);

        Assert.Equal(222, doc.FinalBlowId);
    }

    [Fact]
    public async Task UpsertAsync_TieBreak_IsDeterministic()
    {
        var svc = BuildService();
        var killmail = MakeKillmail((111, 500, false), (222, 500, false));

        var first = await svc.UpsertAsync(killmail, "hash1", 1_000_000m);

        killmail.KillmailId = 2; // different ID to force new upsert
        var second = await svc.UpsertAsync(killmail, "hash1", 1_000_000m);

        Assert.Equal(first.TopDamageId, second.TopDamageId);
    }
}