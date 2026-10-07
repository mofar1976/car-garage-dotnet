using CarGarage.Domain.Entities;
using CarGarage.Domain.Enums;
using CarGarage.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace CarGarage.IntegrationTests;

[Trait("Category", "Database")]
public sealed class UserPersistenceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static User NewUser(string? email = null, AuthAudience audience = AuthAudience.CUSTOMER) => new()
    {
        Email = email ?? $"{Guid.NewGuid():N}@example.test",
        PasswordHash = "test-hash-not-a-real-password",
        FirstName = " Ada ",
        LastName = " Lovelace ",
        Phone = " +4912345 ",
        Audience = audience,
        Roles = [UserRole.CUSTOMER]
    };

    [Fact]
    public async Task MigrationBuildsEmptyDatabaseAndCanBeAppliedAgain()
    {
        await using var context = fixture.CreateContext();
        Assert.True(await context.Database.CanConnectAsync());
        Assert.Equal(2, (await context.Database.GetAppliedMigrationsAsync()).Count());
        await context.Database.MigrateAsync();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync());
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task UserRoundTripNormalizesFieldsAndStoresUtcTimestamps()
    {
        var clock = new TestClock();
        var user = NewUser($"  {Guid.NewGuid():N}@EXAMPLE.TEST  ");
        await using (var context = fixture.CreateContext(clock))
        {
            context.Users.Add(user);
            await context.SaveChangesAsync();
        }
        await using var readContext = fixture.CreateContext();
        var stored = await readContext.Users.SingleAsync(item => item.Id == user.Id);
        Assert.NotEqual(Guid.Empty, stored.Id);
        Assert.Equal(user.Email.Trim().ToLowerInvariant(), stored.Email);
        Assert.Equal("Ada", stored.FirstName);
        Assert.Equal("Lovelace", stored.LastName);
        Assert.Equal("+4912345", stored.Phone);
        Assert.Equal(user.PasswordHash, stored.PasswordHash);
        Assert.Equal(AuthAudience.CUSTOMER, stored.Audience);
        Assert.Equal([UserRole.CUSTOMER], stored.Roles);
        Assert.True(stored.IsActive);
        Assert.False(stored.Deleted);
        Assert.Equal(clock.Now, stored.CreatedAt);
        Assert.Equal(clock.Now, stored.UpdatedAt);
        Assert.Equal(TimeSpan.Zero, stored.CreatedAt.Offset);
    }

    [Fact]
    public async Task UpdatePreservesCreationTimeAndTracksRoleArrayChanges()
    {
        var clock = new TestClock();
        await using var context = fixture.CreateContext(clock);
        var user = NewUser();
        context.Users.Add(user);
        // Also verify the synchronous SaveChanges path.
        context.SaveChanges();
        var createdAt = user.CreatedAt;
        clock.Now = clock.Now.AddHours(1);
        user.FirstName = " Updated ";
        user.Roles[0] = UserRole.WORKSHOP_MEMBER;
        user.CreatedAt = clock.Now;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var stored = await context.Users.SingleAsync(item => item.Id == user.Id);
        Assert.Equal("Updated", stored.FirstName);
        Assert.Equal(createdAt, stored.CreatedAt);
        Assert.Equal(clock.Now, stored.UpdatedAt);
        Assert.Equal([UserRole.WORKSHOP_MEMBER], stored.Roles);
    }

    [Fact]
    public async Task DuplicateNormalizedEmailInSameAudienceIsRejected()
    {
        var email = $"{Guid.NewGuid():N}@example.test";
        await using (var context = fixture.CreateContext())
        {
            context.Users.Add(NewUser(email));
            await context.SaveChangesAsync();
        }
        await using var duplicateContext = fixture.CreateContext();
        duplicateContext.Users.Add(NewUser($" {email.ToUpperInvariant()} "));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => duplicateContext.SaveChangesAsync());
        var postgresError = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgresError.SqlState);
        Assert.Equal("ux_users_email_audience_active", postgresError.ConstraintName);
    }

    [Fact]
    public async Task SameEmailCanBeUsedAcrossAudiencesAndAfterSoftDelete()
    {
        await using var context = fixture.CreateContext();
        var email = $"{Guid.NewGuid():N}@example.test";
        var customer = NewUser(email);
        context.Users.AddRange(customer, NewUser(email, AuthAudience.WORKSHOP));
        await context.SaveChangesAsync();
        customer.Deleted = true;
        await context.SaveChangesAsync();
        context.Users.Add(NewUser(email));
        await context.SaveChangesAsync();
        Assert.Equal(3, await context.Users.CountAsync(user => user.Email == email));
        Assert.Equal(2, await context.Users.CountAsync(user => user.Email == email && !user.Deleted));
    }

    [Theory]
    [InlineData(true, "ck_users_audience")]
    [InlineData(false, "ck_users_roles")]
    public async Task DatabaseRejectsUnknownAudienceOrRole(bool invalidAudience, string constraint)
    {
        await using var context = fixture.CreateContext();
        var user = NewUser();
        if (invalidAudience) user.Audience = (AuthAudience)999;
        else user.Roles = [(UserRole)999];
        context.Users.Add(user);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        var postgresError = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal(PostgresErrorCodes.CheckViolation, postgresError.SqlState);
        Assert.Equal(constraint, postgresError.ConstraintName);
    }

    [Fact]
    public async Task RolledBackTransactionDoesNotPersistUser()
    {
        var user = NewUser();
        await using (var context = fixture.CreateContext())
        {
            await using var transaction = await context.Database.BeginTransactionAsync();
            context.Users.Add(user);
            await context.SaveChangesAsync();
            await transaction.RollbackAsync();
        }
        await using var readContext = fixture.CreateContext();
        Assert.False(await readContext.Users.AnyAsync(item => item.Id == user.Id));
    }

    [Fact]
    public async Task ApiDependencyInjectionResolvesWorkingDatabaseContext()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-only-secret-for-http-tests-32-bytes-minimum",
                ["Password:WorkFactor"] = "4",
                ["Database:ConnectionString"] = fixture.ConnectionString
            }));
        });
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CarGarageDbContext>();
        Assert.True(await context.Database.CanConnectAsync());
    }

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 10, 7, 10, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
