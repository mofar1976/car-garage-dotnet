using CarGarage.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace CarGarage.IntegrationTests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("car_garage_tests")
        .WithUsername("test_user")
        .WithPassword(Guid.NewGuid().ToString("N"))
        .Build();

    public string ConnectionString => container.GetConnectionString();

    public CarGarageDbContext CreateContext(TimeProvider? clock = null) => new(
        new DbContextOptionsBuilder<CarGarageDbContext>().UseNpgsql(ConnectionString).Options,
        clock ?? TimeProvider.System);

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => container.DisposeAsync().AsTask();
}
