using Planner.Api.Services;

namespace Planner.Tests;

public class RoutePlannerTests
{
    private static List<PlanOrder> RandomOrders(int n, int seed)
    {
        var rnd = new Random(seed);
        return Enumerable.Range(1, n)
            .Select(i => new PlanOrder(
                i,
                WeightKg: rnd.NextDouble() * 15 + 0.1,
                VolumeM3: rnd.NextDouble() * 0.05 + 0.001,
                PriceCzk: rnd.Next(50, 1000)))
            .ToList();
    }

    [Fact]
    public void No_route_exceeds_capacity()
    {
        var orders = RandomOrders(200_000, 1);
        var byId = orders.ToDictionary(o => o.Id);
        var routes = RoutePlanner.Plan(orders);

        Assert.True(routes.Count <= RoutePlanner.RouteCount);
        foreach (var route in routes)
        {
            Assert.True(route.Sum(id => byId[id].VolumeM3) <= RoutePlanner.MaxVolumeM3);
            Assert.True(route.Sum(id => byId[id].WeightKg) <= RoutePlanner.MaxWeightKg);
        }
    }

    [Fact]
    public void Empty_input_gives_no_routes()
    {
        Assert.Empty(RoutePlanner.Plan([]));
    }

    [Fact]
    public void Order_larger_than_a_van_is_never_planned()
    {
        var orders = new List<PlanOrder>
        {
            new(1, WeightKg: 10, VolumeM3: RoutePlanner.MaxVolumeM3 + 1, PriceCzk: 1_000_000),
            new(2, WeightKg: RoutePlanner.MaxWeightKg + 1, VolumeM3: 1, PriceCzk: 1_000_000),
            new(3, WeightKg: 10, VolumeM3: 1, PriceCzk: 100)
        };

        var routes = RoutePlanner.Plan(orders);
        Assert.Equal([3L], routes.SelectMany(r => r));
    }

    [Fact]
    public void Smaller_order_fills_the_gap_left_by_one_that_does_not_fit()
    {
        // Best ratio first: A (4 m3) and B (4 m3) cannot share a route, but C (3 m3) fits next to A.
        var orders = new List<PlanOrder>
        {
            new(1, WeightKg: 1, VolumeM3: 4, PriceCzk: 400),
            new(2, WeightKg: 1, VolumeM3: 4, PriceCzk: 390),
            new(3, WeightKg: 1, VolumeM3: 3, PriceCzk: 200)
        };

        var routes = RoutePlanner.Plan(orders);
        Assert.Equal(2, routes.Count);
        Assert.Equal([1L, 3L], routes[0].Order());
        Assert.Equal([2L], routes[1]);
    }

    [Fact]
    public void Never_uses_more_than_the_available_routes()
    {
        // Each order fills a whole van, so only RouteCount of them can be shipped.
        var orders = Enumerable.Range(1, RoutePlanner.RouteCount + 50)
            .Select(i => new PlanOrder(i, WeightKg: 1, VolumeM3: RoutePlanner.MaxVolumeM3, PriceCzk: i))
            .ToList();

        var routes = RoutePlanner.Plan(orders);
        Assert.Equal(RoutePlanner.RouteCount, routes.Count);
        Assert.All(routes, route => Assert.Single(route));
        // The most expensive ones win because they have higher Price (considering that price equals index).
        // This means that we leave first 50 orders unshipped and then ship the limit which is RoutePlanner.RouteCount .
        Assert.All(routes.SelectMany(r => r), id => Assert.True(id > 50));
    }

    [Fact]
    public void Plans_half_a_million_orders_quickly()
    {
        var orders = RandomOrders(500_000, 3);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        RoutePlanner.Plan(orders);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(3), $"Took {sw.Elapsed}");
    }

    [Fact]
    public void Ships_everything_when_it_all_fits()
    {
        var orders = RandomOrders(100, 2);
        Assert.Equal(100, RoutePlanner.Plan(orders).Sum(r => r.Count));
    }
}
