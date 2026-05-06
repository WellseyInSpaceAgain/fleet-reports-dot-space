using FleetReports.Models;

namespace FleetReports.Services;

public interface IFleetSubscriptionRegistry
{
    void Register(FleetSubscription subscription);
}
