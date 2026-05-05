using System.Net;
using System.Text;
using System.Text.Json;
using FleetReports.Models;
using FleetReports.Services;
using Moq;
using Moq.Protected;

namespace FleetReports.Tests;

public class ZkillCharacterFetcherTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2024, 1, 31, 23, 59, 59, DateTimeKind.Utc);

    private static ZkillCharacterFetcher BuildFetcher(
        string zkbJson,
        Mock<IKillmailCacheService> cache,
        Mock<IEsiService> esi)
    {
        var callCount = 0;
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                // First call = page 1 (has data), subsequent = empty (stops pagination)
                var json = callCount == 1 ? zkbJson : ZkbEmpty();
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            });

        var zkbClient = new HttpClient(handler.Object)
        {
            BaseAddress = new Uri("https://zkillboard.com/")
        };

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient("zkb")).Returns(zkbClient);

        var fetcher = new ZkillCharacterFetcher(factory.Object, cache.Object, esi.Object);
        fetcher.RateLimit = () => Task.CompletedTask;
        return fetcher;
    }

    private static string ZkbPage(int killmailId, string hash, decimal value) =>
        JsonSerializer.Serialize(new[]
        {
            new { killmail_id = killmailId, hash, zkb = new { totalValue = value } }
        });

    private static string ZkbEmpty() => "[]";

    [Fact]
    public async Task FetchAsync_DeduplicatesKillmailsAcrossFleetMembers()
    {
        // Two members both appear on the same killmail
        var callCount = 0;
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                // Odd calls = page 1 (has data), even calls = page 2 (empty → stops)
                var json = callCount % 2 == 1 ? ZkbPage(99, "abc", 1_000m) : ZkbEmpty();
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
            });

        var zkbClient = new HttpClient(handler.Object) { BaseAddress = new Uri("https://zkillboard.com/") };
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient("zkb")).Returns(zkbClient);

        var esi = new Mock<IEsiService>();
        esi.Setup(x => x.GetAsync<EsiKillmail>(It.IsAny<string>()))
            .ReturnsAsync(new EsiKillmail
            {
                KillmailId = 99,
                KillmailTime = new DateTime(2024, 1, 15, 12, 0, 0, DateTimeKind.Utc),
                SolarSystemId = 30000142,
                Victim = new EsiKillmailVictim { ShipTypeId = 587 },
                Attackers = []
            });

        var cache = new Mock<IKillmailCacheService>();
        cache.Setup(x => x.GetAsync(It.IsAny<int>())).ReturnsAsync((KillmailDocument?)null);
        cache.Setup(x => x.UpsertAsync(It.IsAny<EsiKillmail>(), It.IsAny<string>(), It.IsAny<decimal>()))
            .ReturnsAsync((EsiKillmail km, string hash, decimal val) => new KillmailDocument
            {
                Id = km.KillmailId,
                Hash = hash,
                KillmailTime = km.KillmailTime
            });

        var fetcher = new ZkillCharacterFetcher(factory.Object, cache.Object, esi.Object);
        fetcher.RateLimit = () => Task.CompletedTask;

        var result = await fetcher.FetchAsync([111, 222], Start, End);

        Assert.Single(result);
        // ESI should only be called once despite two members
        esi.Verify(x => x.GetAsync<EsiKillmail>(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task FetchAsync_DiscardsKillmailsOutsideTimeRange()
    {
        var cache = new Mock<IKillmailCacheService>();
        cache.Setup(x => x.GetAsync(It.IsAny<int>())).ReturnsAsync((KillmailDocument?)null);

        var esi = new Mock<IEsiService>();
        esi.Setup(x => x.GetAsync<EsiKillmail>(It.IsAny<string>()))
            .ReturnsAsync(new EsiKillmail
            {
                KillmailId = 42,
                KillmailTime = new DateTime(2023, 6, 1, 0, 0, 0, DateTimeKind.Utc), // outside range
                SolarSystemId = 30000142,
                Victim = new EsiKillmailVictim { ShipTypeId = 587 },
                Attackers = []
            });
        cache.Setup(x => x.UpsertAsync(It.IsAny<EsiKillmail>(), It.IsAny<string>(), It.IsAny<decimal>()))
            .ReturnsAsync((EsiKillmail km, string hash, decimal val) => new KillmailDocument
            {
                Id = km.KillmailId,
                Hash = hash,
                KillmailTime = km.KillmailTime
            });

        var fetcher = BuildFetcher(ZkbPage(42, "xyz", 500m), cache, esi);
        var result = await fetcher.FetchAsync([111], Start, End);

        Assert.Empty(result);
    }

    [Fact]
    public async Task FetchAsync_CacheHitWithMatchingHash_DoesNotCallEsi()
    {
        var cached = new KillmailDocument
        {
            Id = 77,
            Hash = "matchinghash",
            KillmailTime = new DateTime(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc)
        };

        var cache = new Mock<IKillmailCacheService>();
        cache.Setup(x => x.GetAsync(77)).ReturnsAsync(cached);

        var esi = new Mock<IEsiService>();

        var fetcher = BuildFetcher(ZkbPage(77, "matchinghash", 1_000m), cache, esi);
        var result = await fetcher.FetchAsync([111], Start, End);

        Assert.Single(result);
        esi.Verify(x => x.GetAsync<EsiKillmail>(It.IsAny<string>()), Times.Never);
    }
}
