using CarGarage.Domain.Entities;
using CarGarage.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarGarage.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", table =>
        {
            table.HasCheckConstraint("ck_users_audience", "audience IN ('ADMIN', 'WORKSHOP', 'CUSTOMER')");
            table.HasCheckConstraint("ck_users_roles",
                "roles <@ ARRAY['CUSTOMER', 'WORKSHOP_MEMBER', 'WORKSHOP_ADMIN', 'PLATFORM_ADMIN']::text[] AND array_position(roles, NULL) IS NULL");
            table.HasCheckConstraint("ck_users_email", "email <> '' AND email = lower(btrim(email))");
            table.HasCheckConstraint("ck_users_required_fields",
                "btrim(password_hash) <> '' AND btrim(first_name) <> '' AND btrim(last_name) <> ''");
        });
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(user => user.Email).HasColumnName("email").IsRequired();
        builder.Property(user => user.PasswordHash).HasColumnName("password_hash").IsRequired();
        builder.Property(user => user.FirstName).HasColumnName("first_name").IsRequired();
        builder.Property(user => user.LastName).HasColumnName("last_name").IsRequired();
        builder.Property(user => user.Phone).HasColumnName("phone");
        builder.Property(user => user.Audience).HasColumnName("audience").HasConversion<string>().IsRequired();
        builder.Property(user => user.Roles).HasColumnName("roles").HasColumnType("text[]")
            .HasConversion(
                roles => roles.Select(role => role.ToString()).ToArray(),
                roles => roles.Select(role => Enum.Parse<UserRole>(role)).ToArray(),
                new ValueComparer<UserRole[]>(
                    (left, right) => left != null && right != null && left.SequenceEqual(right),
                    roles => roles.Aggregate(0, (hash, role) => HashCode.Combine(hash, role)),
                    roles => roles.ToArray()))
            .IsRequired();
        builder.Property(user => user.IsActive).HasColumnName("is_active");
        builder.Property(user => user.Deleted).HasColumnName("deleted");
        builder.Property(user => user.CreatedAt).HasColumnName("created_at");
        builder.Property(user => user.UpdatedAt).HasColumnName("updated_at");
        builder.HasIndex(user => new { user.Email, user.Audience })
            .HasDatabaseName("ux_users_email_audience_active").IsUnique().HasFilter("deleted = false");
        // Soft-delete filtering stays explicit, matching the reference backend.
    }
}
