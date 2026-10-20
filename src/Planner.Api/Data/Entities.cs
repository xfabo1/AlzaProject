using NetTopologySuite.Geometries;

namespace Planner.Api.Data;

public class Warehouse
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required Point Geom { get; set; }
}

public class DeliveryRoute
{
    public long Id { get; set; }
    public required int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public required DateOnly PlannedOn { get; set; }
    public bool IsAssignedToVan { get; set; }
}

public class Order
{
    public long Id { get; set; }
    public required double WeightKg { get; set; }
    public required double VolumeM3 { get; set; }
    public required double PriceCzk { get; set; }
    public required Point Geom { get; set; }
    public required int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public long? RouteId { get; set; }
    public DeliveryRoute? Route { get; set; }
}
