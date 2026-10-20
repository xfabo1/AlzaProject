using Microsoft.EntityFrameworkCore;
using Planner.Api.Data;

namespace Planner.Api.Services;

public class RoutePlanService(PlannerDbContext db)
{
    /// <summary>
    ///
    /// </summary>
    public async Task<RoutesResponse?> GetRoutesAsync(int warehouseId, DateOnly date)
    {
        if (!await db.Warehouses.AnyAsync(w => w.Id == warehouseId)) return null;

        await using var tx = await db.Database.BeginTransactionAsync();
        // One planner per warehouse at a time, so two requests cannot pick the same orders.
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({(long)warehouseId})");

        var alreadyPlanned = await db.Routes.AnyAsync(r => r.WarehouseId == warehouseId && r.PlannedOn == date);
        if (!alreadyPlanned) await PlanAsync(warehouseId, date);

        var routeIds = await LoadRoutesAsync(warehouseId, date, onlyFree: false);
        var pending = await db.Orders.CountAsync(o => o.WarehouseId == warehouseId && o.RouteId == null);
        await tx.CommitAsync();

        return new RoutesResponse(warehouseId, date, !alreadyPlanned, pending, routeIds);
    }

    public Task<List<long>> ListRoutesAsync(int warehouseId, DateOnly date, bool onlyFree) =>
        LoadRoutesAsync(warehouseId, date, onlyFree);

    /// <summary>
    /// This method plans the delivery of the orders for specific warehouse and specific date.
    /// It creates up to 240 routes where each route contains all the orders that will be delivered during that route.
    /// </summary>
    /// <param name="warehouseId"></param>
    /// <param name="date"></param>
    private async Task PlanAsync(int warehouseId, DateOnly date)
    {
        //
        var orders = await db.Orders.AsNoTracking()
            .Where(o => o.WarehouseId == warehouseId && o.RouteId == null)
            .Select(o => new PlanOrder(o.Id, o.WeightKg, o.VolumeM3, o.PriceCzk))
            .ToListAsync();

        var plannedRoutes = RoutePlanner.Plan(orders);
        if (plannedRoutes.Count == 0) return;

        // Groups each route with its orders into List of tuples.
        var routes = plannedRoutes
            .Select(ids => (Route: new DeliveryRoute { WarehouseId = warehouseId, PlannedOn = date }, OrderIds: ids))
            .ToList();

        // Makes the ChangeTracker monitor the Route objects and after the DB save, they will contain IDs.
        db.Routes.AddRange(routes.Select(r => r.Route));
        await db.SaveChangesAsync();


        // Flattens the List of tuples where the first element was the route and the second was the List of orders
        // into a List of tuples where first element is the route ID and the second is the one specific order ID.
        // There will be multiple tuples containing the same route ID.
        var assignments = routes
            .SelectMany(r => r.OrderIds.Select(orderId => (OrderId: orderId, RouteId: r.Route.Id)))
            .ToList();

        var orderIds = assignments.Select(a => a.OrderId).ToArray();
        var routeIds = assignments.Select(a => a.RouteId).ToArray();

        // This query updates all the orders with their newly assigned route IDs.
        await db.Database.ExecuteSqlAsync($"""
            UPDATE orders o SET route_id = u.route_id
            FROM unnest({orderIds}, {routeIds}) AS u(id, route_id)
            WHERE o.id = u.id
            """);
    }

    private Task<List<long>> LoadRoutesAsync(int warehouseId, DateOnly date, bool onlyFree) =>
        db.Routes
            .Where(r => r.WarehouseId == warehouseId && r.PlannedOn == date && (!onlyFree || !r.IsAssignedToVan))
            .OrderBy(r => r.Id)
            .Select(r => r.Id)
            .ToListAsync();
}
