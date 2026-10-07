using CarGarage.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarGarage.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens", table =>
        {
            table.HasCheckConstraint("ck_refresh_tokens_audience", "audience IN ('ADMIN', 'WORKSHOP', 'CUSTOMER')");
            table.HasCheckConstraint("ck_refresh_tokens_hash", "token_hash ~ '^[0-9a-f]{64}$'");
        });
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(token => token.UserId).HasColumnName("user_id");
        builder.Property(token => token.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        builder.Property(token => token.Audience).HasColumnName("audience").HasConversion<string>().IsRequired();
        builder.Property(token => token.ExpiresAt).HasColumnName("expires_at");
        builder.Property(token => token.CreatedAt).HasColumnName("created_at");
        builder.Property(token => token.UserAgent).HasColumnName("user_agent");
        builder.HasOne<User>().WithMany().HasForeignKey(token => token.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(token => token.TokenHash).IsUnique().HasDatabaseName("ux_refresh_tokens_hash");
        builder.HasIndex(token => token.ExpiresAt).HasDatabaseName("ix_refresh_tokens_expires_at");
    }
}
