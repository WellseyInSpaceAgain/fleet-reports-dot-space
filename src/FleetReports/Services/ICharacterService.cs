namespace FleetReports.Services;

public interface ICharacterService
{
    Task<Dictionary<string, int>> ResolveNamesAsync(IEnumerable<string> names);
}
