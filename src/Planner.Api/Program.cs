using Microsoft.EntityFrameworkCore;
using Planner.Api.Data;
using Planner.Api.Services;

namespace Planner.Api;

public abstract class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddDbContext<PlannerDbContext>(o => o
            .UseNpgsql(builder.Configuration.GetConnectionString("Db"), n => n.UseNetTopologySuite())
            .UseSnakeCaseNamingConvention());
        builder.Services.AddControllers();
        builder.Services.AddScoped<RoutePlanService>();

        var app = builder.Build();

        await app.InitializeDatabaseAsync();
        app.MapControllers();

        await app.RunAsync();
    }
}
