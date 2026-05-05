using FleetReports.Services;
using Moq;

namespace FleetReports.Tests;

public class CharacterServiceTests
{
    [Fact]
    public async Task ResolveNamesAsync_ReturnsNameToIdMapping()
    {
        var mock = new Mock<IEsiService>();
        mock.Setup(x => x.PostAsync<UniverseIdsResponse>("universe/ids/", It.IsAny<object>()))
        .ReturnsAsync(new UniverseIdsResponse([
            new CharacterEntry(12345, "Wellsey"),
            new CharacterEntry(67890, "Anotherchar"),
        ]));

        var service = new CharacterService(mock.Object);
        var result = await service.ResolveNamesAsync(["Wellsey", "Anotherchar"]);

        Assert.Equal(12345, result["Wellsey"]);
        Assert.Equal(67890, result["Anotherchar"]);
    }

    [Fact]
    public async Task ResolveNamesAsync_ExcludesUnknownNames()
    {
        var mock = new Mock<IEsiService>();
        mock.Setup(x => x.PostAsync<UniverseIdsResponse>("universe/ids/", It.IsAny<object>()))
            .ReturnsAsync(new UniverseIdsResponse([
                new CharacterEntry(12345, "Wellsey")
            ]));

        var service = new CharacterService(mock.Object);
        var result = await service.ResolveNamesAsync(["Wellsey", "NotARealPlayer"]);

        Assert.Single(result);
        Assert.False(result.ContainsKey("NotARealPlayer"));
    }
}
