namespace FleetReports.Services;

public interface IReportService
{
    Task<string> CreateReportAsync(string[] names, DateTime startTime, DateTime endTime, IProgress<string>? progress = null);
} 
