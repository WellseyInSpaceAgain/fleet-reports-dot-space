namespace FleetReports.Services;

public interface IEsiService
{
    Task<T?> GetAsync<T>(string path);
    Task<T?> PostAsync<T>(string path, object body);
}
