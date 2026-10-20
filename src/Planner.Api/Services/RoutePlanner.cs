namespace Planner.Api.Services;

/// <summary>The part of an order the planner needs.</summary>
public record PlanOrder(long Id, double WeightKg, double VolumeM3, double PriceCzk);

public static class RoutePlanner
{
    public const int Vans = 120;
    public const int RoutesPerVan = 2;
    public const int RouteCount = Vans * RoutesPerVan;
    public const double MaxVolumeM3 = 7.0;
    public const double MaxWeightKg = 5500.0;

    /// <summary>
    /// Splits the orders into routes. Every route is the list of ids of the orders loaded onto it.
    /// </summary>
    /// <returns>The two dimension list where each inner list contains the orders for one specific route.</returns>
    public static List<List<long>> Plan(IEnumerable<PlanOrder> orders)
    {
        var remaining = Rank(orders);
        var routes = new List<List<long>>();

        if (remaining.Count == 0) return routes;

        var smallestVolume = remaining.Min(o => o.VolumeM3);
        var smallestWeight = remaining.Min(o => o.WeightKg);

        while (routes.Count < RouteCount && remaining.Count > 0)
        {
            var route = FillRoute(remaining, smallestVolume, smallestWeight);

            routes.Add(route.Loaded.Select(o => o.Id).ToList());
            remaining = route.NotLoaded;
        }

        return routes;
    }

    /// <summary>
    /// Orders that can fit into an empty van, the best price per used space of the van first.
    /// The used space is whichever of volume and weight fills more of the van.
    /// </summary>
    private static List<PlanOrder> Rank(IEnumerable<PlanOrder> orders)
    {
        return orders
            .Where(o => o.VolumeM3 <= MaxVolumeM3 && o.WeightKg <= MaxWeightKg)
            .OrderByDescending(o => o.PriceCzk / Math.Max(o.VolumeM3 / MaxVolumeM3, o.WeightKg / MaxWeightKg))
            .ToList();
    }

    private static (List<PlanOrder> Loaded, List<PlanOrder> NotLoaded) FillRoute(
        List<PlanOrder> candidates, double smallestVolume, double smallestWeight)
    {
        var loaded = new List<PlanOrder>();
        var notLoaded = new List<PlanOrder>();
        double volume = 0, weight = 0;
        var next = 0;

        // Once not even the smallest order fits, there is no point in looking further.
        while (next < candidates.Count
               && MaxVolumeM3 - volume >= smallestVolume
               && MaxWeightKg - weight >= smallestWeight)
        {
            var order = candidates[next++];

            if (volume + order.VolumeM3 <= MaxVolumeM3 && weight + order.WeightKg <= MaxWeightKg)
            {
                loaded.Add(order);
                volume += order.VolumeM3;
                weight += order.WeightKg;
            }
            else
            {
                notLoaded.Add(order);
            }
        }

        notLoaded.AddRange(candidates.GetRange(next, candidates.Count - next));   // never looked at
        return (loaded, notLoaded);
    }
}
