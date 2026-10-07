using CarGarage.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CarGarage.Infrastructure.Persistence;

public sealed class CarGarageDbContextFactory : IDesignTimeDbContextFactory<CarGarageDbContext>
{
    public CarGarageDbContext CreateDbContext(string[] args)
    {
        var settings = new DatabaseOptions
        {
            ConnectionString = Environment.GetEnvironmentVariable("Database__ConnectionString") ?? ""
        };
        if (!settings.IsValid())
            throw new InvalidOperationException("Set Database__ConnectionString with Host, Database and Username before using dotnet ef.");

        var options = new DbContextOptionsBuilder<CarGarageDbContext>()
            .UseNpgsql(settings.ConnectionString).Options;
        return new CarGarageDbContext(options, TimeProvider.System);
    }
}
