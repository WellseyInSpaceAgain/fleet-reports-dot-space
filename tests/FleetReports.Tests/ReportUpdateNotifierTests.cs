using FleetReports.Services;

namespace FleetReports.Tests;

public class ReportUpdateNotifierTests
{
    [Fact]
    public void Subscribe_ThenNotify_CallbackFires()
    {
        var notifier = new ReportUpdateNotifier();
        var called = false;

        notifier.Subscribe("report1", () => called = true);
        notifier.Notify("report1");

        Assert.True(called);
    }

    [Fact]
    public void Unsubscribe_ThenNotify_CallbackNotFired()
    {
        var notifier = new ReportUpdateNotifier();
        var called = false;

        notifier.Subscribe("report1", () => called = true);
        notifier.Unsubscribe("report1");
        notifier.Notify("report1");

        Assert.False(called);
    }

    [Fact]
    public void MultipleSubscribers_SameReportId_AllFire()
    {
        var notifier = new ReportUpdateNotifier();
        var count = 0;

        notifier.Subscribe("report1", () => count++);
        notifier.Subscribe("report1", () => count++);
        notifier.Notify("report1");

        Assert.Equal(2, count);
    }

    [Fact]
    public void Notify_UnknownReportId_DoesNotThrow()
    {
        var notifier = new ReportUpdateNotifier();
        notifier.Notify("does-not-exist");
    }
}
