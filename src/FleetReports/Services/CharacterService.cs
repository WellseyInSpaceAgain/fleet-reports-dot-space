namespace FleetReports.Services;

public class CharacterService(IEsiService esi) : ICharacterService
{
    private readonly IEsiService _esi = esi;

    public async Task<Dictionary<string, int>> ResolveNamesAsync(IEnumerable<string> names)
    {
        var result = await _esi.PostAsync<UniverseIdsResponse>("universe/ids/", names);
        return result?.Characters.ToDictionary(c => c.Name, c => c.Id) ?? [];
    }
}
