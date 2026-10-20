using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Planner.Api;
using Planner.Api.Data;
using Testcontainers.PostgreSql;

namespace Planner.Tests.E2E;

/// <summary>
/// Starts a throwaway PostGIS container and the real API on top of it, once for all the E2E tests.
/// Docker has to be running.
/// </summary>
public class ApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgis/postgis:16-3.4").Build();
    private WebApplicationFactory<Program> _factory = null!;

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _db.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("ConnectionStrings:Db", _db.GetConnectionString());
            b.UseSetting("Seed:Orders", "0");   // every test creates its own data
        });
        Client = _factory.CreateClient();   // starts the app, which creates the tables
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await _factory.DisposeAsync();
        await _db.DisposeAsync();
    }

    /// <summary>Gives the test direct access to the database, for data that is too slow to create through the API.</summary>
    public async Task WithDbAsync(Func<PlannerDbContext, Task> action)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<PlannerDbContext>());
    }

    /// <summary>Removes all the orders and routes, the warehouses stay.</summary>
    public Task ResetAsync() =>
        WithDbAsync( db => db.Database.ExecuteSqlRawAsync("TRUNCATE orders, routes RESTART IDENTITY"));
}

[CollectionDefinition("E2E")]
public class E2ECollection : ICollectionFixture<ApiFixture>;
