using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using Planner.Api.Data;

namespace Planner.Api.Controllers;

[ApiController]
[Route("orders")]
public class OrdersController(PlannerDbContext db) : ControllerBase
{
    private static readonly GeometryFactory Factory = NtsGeometryServices.Instance.CreateGeometryFactory(4326);

    /// <summary>
    /// Creates order for that give OrderRequest.
    /// </summary>
    /// <param name="request">Object containing all the information about the order.</param>
    /// <returns></returns>
    [HttpPost]
    public async Task<ActionResult<OrderCreated>> CreateOrder(OrderRequest request)
    {
        var location = Factory.CreatePoint(new Coordinate(request.Lon, request.Lat));
        var warehouse = await db.Warehouses.OrderBy(w => w.Geom.Distance(location)).FirstAsync();

        var order = new Order
        {
            WeightKg = request.WeightKg, VolumeM3 = request.VolumeM3, PriceCzk = request.PriceCzk,
            Geom = location, WarehouseId = warehouse.Id
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, new OrderCreated(order.Id, warehouse.Id, warehouse.Name));
    }

    /// <summary>
    /// Gets order by Order ID.
    /// </summary>
    /// <returns></returns>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<OrderDto>> GetOrder(long id)
    {
        var order = await db.Orders.Where(o => o.Id == id)
            .Select(o => new OrderDto(o.Id, o.WeightKg, o.VolumeM3, o.PriceCzk, o.Warehouse!.Name, o.RouteId))
            .FirstOrDefaultAsync();
        return order is null ? NotFound() : order;
    }
}
