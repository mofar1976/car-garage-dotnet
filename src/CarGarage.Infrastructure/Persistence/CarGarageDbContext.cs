using CarGarage.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarGarage.Infrastructure.Persistence;

public sealed class CarGarageDbContext(DbContextOptions<CarGarageDbContext> options, TimeProvider clock)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CarGarageDbContext).Assembly);

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareUsers();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        PrepareUsers();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void PrepareUsers()
    {
        var now = clock.GetUtcNow();
        foreach (var entry in ChangeTracker.Entries<User>()
                     .Where(entry => entry.State is EntityState.Added or EntityState.Modified))
        {
            entry.Entity.Email = entry.Entity.Email.Trim().ToLowerInvariant();
            entry.Entity.FirstName = entry.Entity.FirstName.Trim();
            entry.Entity.LastName = entry.Entity.LastName.Trim();
            entry.Entity.Phone = entry.Entity.Phone?.Trim();
            if (entry.State == EntityState.Added)
                entry.Entity.CreatedAt = now;
            else
                entry.Property(user => user.CreatedAt).IsModified = false;
            entry.Entity.UpdatedAt = now;
        }
    }
}
