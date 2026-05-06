namespace FleetReports.Services;

public interface IReportUpdateNotifier
{
    void Subscribe(string reportId, Action callback);
    void Unsubscribe(string reportId);
    void Notify(string reportId);
}
