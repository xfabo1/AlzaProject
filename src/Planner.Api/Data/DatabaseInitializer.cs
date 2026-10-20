using Microsoft.EntityFrameworkCore;

namespace Planner.Api.Data;

public static class DatabaseInitializer
{
    /// <summary>Creates the tables if the database is empty and optionally fills it with demo orders ("Seed:Orders").</summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlannerDbContext>();

        await db.Database.EnsureCreatedAsync();

        var count = app.Configuration.GetValue<int>("Seed:Orders");
        if (count > 0 && !await db.Orders.AnyAsync())
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO orders (weight_kg, volume_m3, price_czk, geom, warehouse_id)
                SELECT s.weight, s.volume, s.price, s.geom,
                       (SELECT w.id FROM warehouses w ORDER BY w.geom <-> s.geom LIMIT 1)
                FROM (
                    SELECT 0.1 + random() * 15     AS weight,
                           0.001 + random() * 0.05 AS volume,
                           50 + random() * 1000    AS price,
                           ST_MakePoint(12.1 + random() * 6.7, 48.6 + random() * 2.4)::geography AS geom
                    FROM generate_series(1, {count})
                ) s
                """);
    }
}
