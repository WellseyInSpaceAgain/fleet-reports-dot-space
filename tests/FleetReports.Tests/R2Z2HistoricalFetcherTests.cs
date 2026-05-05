using System.Net;
using System.Text;
using System.Text.Json;
using FleetReports.Models;
using FleetReports.Services;
using Moq;
using Moq.Protected;

namespace FleetReports.Tests;

public class R2Z2HistoricalFetcherTests
{
    private static readonly DateTime Start = new(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2024, 1, 20, 23, 59, 59, DateTimeKind.Utc);

    private static R2Z2HistoricalFetcher BuildFetcher(
        IEnumerable<(HttpStatusCode status, string json)> responses,
        Mock<IKillmailCacheService> cache)
    {
        var queue = new Queue<(HttpStatusCode, string)>(responses);

        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                var (status, json) = queue.Dequeue();
                return new HttpResponseMessage
                {
                    StatusCode = status,
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            });

        var client = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://r2z2.zkillboard.com/ephemeral/")
        };

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient("r2z2")).Returns(client);

        return new R2Z2HistoricalFetcher(factory.Object, cache.Object);
    }

    private static string SeqJson(int seq) =>
        JsonSerializer.Serialize(new { sequence = seq });

    private static string EntryJson(int killmailId, DateTime time, int? victimCharId = null, int[]? attackerIds = null) =>
        JsonSerializer.Serialize(new
        {
            killmail_id = killmailId,
            killmail_time = time,
            solar_system_id = 30000142,
            victim = new { character_id = victimCharId, ship_type_id = 587 },
            attackers = (attackerIds ?? []).Select(id => new { character_id = (int?)id, damage_done = 100, final_blow = false }).ToArray(),
            zkb = new { hash = "abc123", totalValue = 1_000_000m }
        });

    [Fact]
    public async Task FetchAsync_NoFleetMemberMatch_DoesNotUpsert()
    {
        var cache = new Mock<IKillmailCacheService>();
        cache.Setup(x => x.UpsertAsync(It.IsAny<EsiKillmail>(), It.IsAny<string>(), It.IsAny<decimal>()))
            .ReturnsAsync(new KillmailDocument());

        var fetcher = BuildFetcher([
            (HttpStatusCode.OK, SeqJson(100)),
            (HttpStatusCode.OK, EntryJson(1, new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc), victimCharId: 999, attackerIds: [888])),
            (HttpStatusCode.NotFound, "")
        ], cache);

        var result = await fetcher.FetchAsync([111, 222], Start, End);

        Assert.Empty(result);
        cache.Verify(x => x.UpsertAsync(It.IsAny<EsiKillmail>(), It.IsAny<string>(), It.IsAny<decimal>()), Times.Never);
    }

    [Fact]
    public async Task FetchAsync_FleetMemberIsVictim_UpsertsCalled()
    {
        var cache = new Mock<IKillmailCacheService>();
        cache.Setup(x => x.UpsertAsync(It.IsAny<EsiKillmail>(), It.IsAny<string>(), It.IsAny<decimal>()))
            .ReturnsAsync((EsiKillmail km, string hash, decimal val) => new KillmailDocument { Id = km.KillmailId, Hash = hash });

        var fetcher = BuildFetcher([
            (HttpStatusCode.OK, SeqJson(100)),
            (HttpStatusCode.OK, EntryJson(1, new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc), victimCharId: 111)),
            (HttpStatusCode.NotFound, "")
        ], cache);

        var result = await fetcher.FetchAsync([111], Start, End);

        Assert.Single(result);
        cache.Verify(x => x.UpsertAsync(It.IsAny<EsiKillmail>(), "abc123", 1_000_000m), Times.Once);
    }

    [Fact]
    public async Task FetchAsync_StopsWalk_WhenKillmailTimeBeforeStartTime()
    {
        var cache = new Mock<IKillmailCacheService>();
        cache.Setup(x => x.UpsertAsync(It.IsAny<EsiKillmail>(), It.IsAny<string>(), It.IsAny<decimal>()))
            .ReturnsAsync((EsiKillmail km, string hash, decimal val) => new KillmailDocument { Id = km.KillmailId, Hash = hash });

        var fetcher = BuildFetcher([
            (HttpStatusCode.OK, SeqJson(100)),
            // seq 100: within range, fleet member attacker
            (HttpStatusCode.OK, EntryJson(1, new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc), attackerIds: [111])),
            // seq 99: before start time — should stop walk
            (HttpStatusCode.OK, EntryJson(2, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), attackerIds: [111])),
        ], cache);

        var result = await fetcher.FetchAsync([111], Start, End);

        // Only the in-range kill should be returned; walk stopped before upsert of second entry
        Assert.Single(result);
        cache.Verify(x => x.UpsertAsync(It.IsAny<EsiKillmail>(), It.IsAny<string>(), It.IsAny<decimal>()), Times.Once);
    }
}
