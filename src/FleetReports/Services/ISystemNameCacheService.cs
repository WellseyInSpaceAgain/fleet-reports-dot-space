namespace FleetReports.Services;

public interface ISystemNameCacheService
{
    Task<string> GetOrResolveAsync(int systemId);
}
