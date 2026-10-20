using System.Net;
using System.Net.Http.Json;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using Planner.Api;
using Planner.Api.Data;
using Planner.Api.Services;

namespace Planner.Tests.E2E;

[Collection("E2E")]
[Trait("Category", "E2E")]
public class ApiTests(ApiFixture fx) : IAsyncLifetime
{
    private const int Praha = 1;
    private const int Brno = 2;
    private const string Day = "2026-10-20";
    private const string NextDay = "2026-10-21";

    public Task InitializeAsync() => fx.ResetAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Order_is_matched_to_the_nearest_warehouse()
    {
        var praha = await CreateOrderAsync(lat: 50.08, lon: 14.42);
        var brno = await CreateOrderAsync(lat: 49.19, lon: 16.61);

        Assert.Equal((Praha, "Praha"), (praha.WarehouseId, praha.Warehouse));
        Assert.Equal((Brno, "Brno"), (brno.WarehouseId, brno.Warehouse));

        var stored = await fx.Client.GetFromJsonAsync<OrderDto>($"/orders/{praha.Id}");
        Assert.Equal("Praha", stored!.Warehouse);
        Assert.Null(stored.RouteId);   // not planned yet
    }

    [Fact]
    public async Task Planning_a_day_puts_every_order_on_a_route_and_is_idempotent()
    {
        var created = new List<long>();
        for (var i = 0; i < 5; i++) created.Add((await CreateOrderAsync()).Id);

        var first = await PlanAsync(Praha, Day);
        Assert.True(first.PlannedNow);
        Assert.Equal(0, first.StillPending);

        var planned = new List<long>();
        foreach (var routeId in first.RouteIds)
        {
            var route = await fx.Client.GetFromJsonAsync<RouteDto>($"/routes/{routeId}");
            planned.AddRange(route!.Orders.Select(o => o.Id));
        }
        Assert.Equal(created.Order(), planned.Order());

        var again = await PlanAsync(Praha, Day);
        Assert.False(again.PlannedNow);
        Assert.Equal(first.RouteIds, again.RouteIds);

        var stored = await fx.Client.GetFromJsonAsync<OrderDto>($"/orders/{created[0]}");
        Assert.NotNull(stored!.RouteId);
    }

    [Fact]
    public async Task Only_the_first_driver_gets_a_route()
    {
        await CreateOrderAsync();
        var routeId = (await PlanAsync(Praha, Day)).RouteIds.Single();

        Assert.Equal(HttpStatusCode.NoContent, (await fx.Client.PostAsync($"/routes/{routeId}/assign", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await fx.Client.PostAsync($"/routes/{routeId}/assign", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fx.Client.PostAsync("/routes/999999/assign", null)).StatusCode);

        var free = await fx.Client.GetFromJsonAsync<List<long>>($"/routes?warehouseId={Praha}&date={Day}");
        var all = await fx.Client.GetFromJsonAsync<List<long>>($"/routes?warehouseId={Praha}&date={Day}&onlyFree=false");
        Assert.Empty(free!);
        Assert.Equal([routeId], all);
    }

    [Fact]
    public async Task Orders_beyond_the_available_vans_stay_open_for_the_next_day()
    {
        // Every order fills a whole van, so only RouteCount of them fit into one day.
        const int extra = 10;
        await InsertOrdersAsync(Praha, RoutePlanner.RouteCount + extra, volumeM3: RoutePlanner.MaxVolumeM3);

        var day1 = await PlanAsync(Praha, Day);
        Assert.Equal(RoutePlanner.RouteCount, day1.RouteIds.Count);
        Assert.Equal(extra, day1.StillPending);

        var praha = (await fx.Client.GetFromJsonAsync<List<WarehouseDto>>("/warehouses"))!.Single(w => w.Id == Praha);
        Assert.Equal(RoutePlanner.RouteCount + extra, praha.TotalOrders);
        Assert.Equal(RoutePlanner.RouteCount, praha.PlannedOrders);
        Assert.Equal(extra, praha.OpenOrders);

        var day2 = await PlanAsync(Praha, NextDay);
        Assert.Equal(extra, day2.RouteIds.Count);
        Assert.Equal(0, day2.StillPending);
    }

    [Fact]
    public async Task Invalid_requests_are_rejected()
    {
        var badOrder = await fx.Client.PostAsJsonAsync("/orders", new OrderRequest(0, 0.1, 500, 50.08, 14.42));
        Assert.Equal(HttpStatusCode.BadRequest, badOrder.StatusCode);

        var unknownWarehouse = await fx.Client.GetAsync($"/warehouses/999/routes?date={Day}");
        Assert.Equal(HttpStatusCode.NotFound, unknownWarehouse.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await fx.Client.GetAsync("/orders/999999")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await fx.Client.GetAsync("/routes/999999")).StatusCode);
    }

    private async Task<OrderCreated> CreateOrderAsync(double lat = 50.08, double lon = 14.42)
    {
        var response = await fx.Client.PostAsJsonAsync("/orders", new OrderRequest(10, 0.1, 500, lat, lon));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<OrderCreated>())!;
    }

    private async Task<RoutesResponse> PlanAsync(int warehouseId, string date)
    {
        var response = await fx.Client.GetAsync($"/warehouses/{warehouseId}/routes?date={date}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<RoutesResponse>())!;
    }

    /// <summary>Creating hundreds of orders through the API would be slow, so these go straight into the database.</summary>
    private Task InsertOrdersAsync(int warehouseId, int count, double volumeM3)
    {
        var location = NtsGeometryServices.Instance.CreateGeometryFactory(4326).CreatePoint(new Coordinate(14.42, 50.08));
        return fx.WithDbAsync(async db =>
        {
            db.Orders.AddRange(Enumerable.Range(1, count).Select(i => new Order
            {
                WeightKg = 1, VolumeM3 = volumeM3, PriceCzk = i, Geom = location, WarehouseId = warehouseId
            }));
            await db.SaveChangesAsync();
        });
    }
}
