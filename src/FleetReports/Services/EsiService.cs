namespace FleetReports.Services;

public class EsiService : IEsiService
{
    private readonly HttpClient _client;

    public EsiService(HttpClient client, IConfiguration config)
    {
        client.BaseAddress = new Uri("https://esi.evetech.net/latest/");
        client.DefaultRequestHeaders.Add("User-Agent", config["Esi:UserAgent"]);
        client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip");
        _client = client;
    }

    public async Task<T?> GetAsync<T>(string path)
    {
        var response = await _client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }

    public async Task<T?> PostAsync<T>(string path, object body)
    {
        var response = await _client.PostAsJsonAsync(path, body);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }
}
