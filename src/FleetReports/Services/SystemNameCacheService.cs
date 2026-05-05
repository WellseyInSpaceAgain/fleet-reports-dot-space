using System.Collections.Concurrent;
using FleetReports.Models;

namespace FleetReports.Services;

public class SystemNameCacheService(IHttpClientFactory httpClientFactory) : ISystemNameCacheService
{
    private readonly ConcurrentDictionary<int, string> _cache = new();
    public async Task<string> GetOrResolveAsync(int systemId)
    {
        if (_cache.TryGetValue(systemId, out var name))
        {
            return name;
        }

        var client = httpClientFactory.CreateClient("esi");
        var response = await client.GetFromJsonAsync<EsiSystemResponse>($"universe/systems/{systemId}/");

        var resolved = response?.Name ?? systemId.ToString();
        _cache[systemId] = resolved;
        return resolved;
    }
}
