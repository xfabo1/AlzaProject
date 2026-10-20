using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace Planner.Api.Data;

public class PlannerDbContext(DbContextOptions<PlannerDbContext> options) : DbContext(options)
{
    private static readonly GeometryFactory Factory = NtsGeometryServices.Instance.CreateGeometryFactory(4326);

    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<DeliveryRoute> Routes => Set<DeliveryRoute>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasPostgresExtension("postgis");

        b.Entity<Warehouse>(e =>
        {
            e.ToTable("warehouses");
            e.Property(x => x.Geom).HasColumnType("geography(Point, 4326)");
            e.HasIndex(x => x.Geom).HasMethod("gist");

            e.HasData(
                W(1, "Praha", 14.4214, 50.0875), W(2, "Brno", 16.6083, 49.1953),
                W(3, "Ostrava", 18.2925, 49.8356), W(4, "Plzen", 13.3825, 49.7414),
                W(5, "Ceske Budejovice", 14.4747, 48.9747), W(6, "Liberec", 15.0500, 50.7667),
                W(7, "Hradec Kralove", 15.8319, 50.2092), W(8, "Olomouc", 17.2508, 49.5939),
                W(9, "Jihlava", 15.5906, 49.4003), W(10, "Karlovy Vary", 12.8725, 50.2306),
                W(11, "Usti nad Labem", 14.0417, 50.6592), W(12, "Pardubice", 15.7792, 50.0386),
                W(13, "Zlin", 17.6669, 49.2331));
        });

        b.Entity<Order>(e =>
        {
            e.Property(x => x.Geom).HasColumnType("geography(Point, 4326)");
            e.HasIndex(x => x.WarehouseId);

            // A warehouse with orders must not be deletable, and its orders must never be removed with it.
            e.HasOne(x => x.Warehouse).WithMany()
                .HasForeignKey(x => x.WarehouseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<DeliveryRoute>(e =>
        {
            e.ToTable("routes");
            e.HasIndex(x => new { x.WarehouseId, x.PlannedOn });
        });
    }

    private static Warehouse W(int id, string name, double lon, double lat) =>
        new() { Id = id, Name = name, Geom = Factory.CreatePoint(new Coordinate(lon, lat)) };
}
