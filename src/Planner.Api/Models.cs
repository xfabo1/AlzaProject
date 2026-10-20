using System.ComponentModel.DataAnnotations;

namespace Planner.Api;

public record OrderRequest(
    [Range(0.001, 5500)] double WeightKg,
    [Range(0.000001, 7)] double VolumeM3,
    [Range(0, double.MaxValue)] double PriceCzk,
    [Range(48, 52)] double Lat,
    [Range(12, 19)] double Lon);
public record OrderCreated(long Id, int WarehouseId, string Warehouse);
public record OrderDto(long Id, double WeightKg, double VolumeM3, double PriceCzk, string Warehouse, long? RouteId);

public record WarehouseDto(int Id, string Name, double Lat, double Lon, int TotalOrders, int PlannedOrders, int OpenOrders);

public record RouteDto(long Id, int WarehouseId, DateOnly PlannedOn, bool IsAssignedToVan, List<OrderDto> Orders);
public record RoutesResponse(int WarehouseId, DateOnly Date, bool PlannedNow, int StillPending, List<long> RouteIds);
