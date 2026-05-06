using System.Collections.Concurrent;

namespace FleetReports.Services;

public class ReportUpdateNotifier : IReportUpdateNotifier
{
    private ConcurrentDictionary<string, List<Action>> _subscribers = new();

    public void Notify(string reportId)
    {
        _subscribers.TryGetValue(reportId, out var list);
        if (list is null)
        {
            return;
        }

        Action[]? localList = [];

        lock (list)
        {
            localList = list.ToArray();
        }
        
        foreach(var callback in localList)
        {
            try
            {
                callback.Invoke();
            }
            catch (Exception)
            {
                continue;
            }
        }
    }

    public void Subscribe(string reportId, Action callback)
    {
        var list = _subscribers.GetOrAdd(reportId, _ => new List<Action>());
        lock(list)
        {
            list.Add(callback);
        }
    }

    public void Unsubscribe(string reportId)
    {
        _subscribers.TryRemove(reportId, out _);
    }
}
