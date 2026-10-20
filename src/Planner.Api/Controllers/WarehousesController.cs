using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Planner.Api.Data;
using Planner.Api.Services;

namespace Planner.Api.Controllers;

[ApiController]
[Route("warehouses")]
public class WarehousesController(PlannerDbContext db, RoutePlanService planner) : ControllerBase
{

    /// <summary>
    /// Lists all the warehouse together with information about the warehouse.
    /// Warehouse contains information such as ID, name, location, total orders, number of prepared orders
    /// for shipping and the number of not yet prepared orders for shipping.
    /// </summary>
    [HttpGet]
    public async Task<List<WarehouseDto>> ListWarehouses()
    {
        var warehouses = await db.Warehouses.AsNoTracking().OrderBy(w => w.Id).ToListAsync();
        var counts = await db.Orders
            .GroupBy(o => o.WarehouseId)
            .Select(g => new { WarehouseId = g.Key, Total = g.Count(), Planned = g.Count(o => o.RouteId != null) })
            .ToDictionaryAsync(x => x.WarehouseId);

        return warehouses
            .Select(w =>
            {
                var c = counts.GetValueOrDefault(w.Id);
                var total = c?.Total ?? 0;
                var planned = c?.Planned ?? 0;
                return new WarehouseDto(w.Id, w.Name, w.Geom.Y, w.Geom.X, total, planned, total - planned);
            })
            .ToList();
    }

    [HttpGet("{id:int}/routes")]
    public async Task<ActionResult<RoutesResponse>> GetRoutes(int id, DateOnly date)
    {
        var result = await planner.GetRoutesAsync(id, date);
        return result is null ? NotFound("Unknown warehouse.") : result;
    }
}
