using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MultiTenantSaaS.Domain.Entities;
using MultiTenantSaaS.Domain.Enums;

namespace MultiTenantSaaS.Infrastructure.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    /// <summary>
    /// Fixed date for seeded rows. DateTime.UtcNow here would make every `migrations add`
    /// produce a new, pointless migration.
    /// </summary>
    public static readonly DateTime SeedDateUtc = new(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Slug).IsRequired().HasMaxLength(63);
        builder.Property(t => t.Plan).HasConversion<int>();

        builder.HasIndex(t => t.Slug).IsUnique();

        // Ensures the system tenant row exists before any real tenant.
        builder.HasData(new
        {
            Id = Tenant.SystemTenantId,
            Name = "System",
            Slug = Tenant.SystemTenantSlug,
            Plan = SubscriptionPlan.Enterprise,
            IsActive = true,
            RequestsPerMinuteOverride = (int?)null,
            CreatedAtUtc = SeedDateUtc,
            UpdatedAtUtc = (DateTime?)null
        });
    }
}
