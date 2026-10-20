using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Planner.Api.Data;
using Planner.Api.Services;

namespace Planner.Api.Controllers;

[ApiController]
[Route("routes")]
public class RoutesController(PlannerDbContext db, RoutePlanService planner) : ControllerBase
{
    /// <summary>
    /// Returns routes planned for a warehouse and day, by default only the ones nobody has picked yet.
    /// </summary>
    [HttpGet]
    public Task<List<long>> ListRoutes(int warehouseId, DateOnly date, bool onlyFree = true) =>
        planner.ListRoutesAsync(warehouseId, date, onlyFree);

    [HttpGet("{id:long}")]
    public async Task<ActionResult<RouteDto>> GetRoute(long id)
    {
        var route = await db.Routes.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
        if (route is null) return NotFound();

        var orders = await db.Orders
            .Where(o => o.RouteId == id)
            .Select(o => new OrderDto(o.Id, o.WeightKg, o.VolumeM3, o.PriceCzk, o.Warehouse!.Name, o.RouteId))
            .ToListAsync();

        return new RouteDto(route.Id, route.WarehouseId, route.PlannedOn, route.IsAssignedToVan, orders);
    }

    /// <summary>
    /// Assigns driver a route. Only the first one to ask gets it, everyone else gets 409.
    /// If the route for that specific ID does not exists, returns 404.
    /// </summary>
    [HttpPost("{id:long}/assign")]
    public async Task<IActionResult> Assign(long id)
    {
        // One conditional UPDATE, so two drivers can never pick the same route.
        var picked = await db.Routes
            .Where(r => r.Id == id && !r.IsAssignedToVan)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsAssignedToVan, true));
        if (picked == 1) return NoContent();

        return await db.Routes.AnyAsync(r => r.Id == id)
            ? Conflict("Route is already assigned to a van.")
            : NotFound();
    }
}
