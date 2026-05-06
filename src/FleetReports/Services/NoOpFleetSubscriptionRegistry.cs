using FleetReports.Models;

namespace FleetReports.Services;

public class NoOpFleetSubscriptionRegistry : IFleetSubscriptionRegistry
{
    public void Register(FleetSubscription subscription) { }
}
