namespace FleetReports.Services;

public class EsiService(IHttpClientFactory httpClientFactory) : IEsiService
{
    public async Task<T?> GetAsync<T>(string path)
    {
        var client = httpClientFactory.CreateClient("esi");
        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }

    public async Task<T?> PostAsync<T>(string path, object body)
    {
        var client = httpClientFactory.CreateClient("esi");
        var response = await client.PostAsJsonAsync(path, body);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }
}
