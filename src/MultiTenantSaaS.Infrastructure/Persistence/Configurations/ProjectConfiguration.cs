using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MultiTenantSaaS.Domain.Entities;

namespace MultiTenantSaaS.Infrastructure.Persistence.Configurations;

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Code).IsRequired().HasMaxLength(10);
        builder.Property(p => p.Description).HasMaxLength(2000);

        builder.HasIndex(p => new { p.TenantId, p.Code }).IsUnique();
        builder.HasIndex(p => new { p.TenantId, p.CreatedAtUtc });

        // Alternate key (TenantId, Id): the composite target of the foreign key from Tickets.
        // Without it, a ticket could reference another tenant's project at the database level,
        // since EF filters do not apply to referential integrity.
        builder.HasAlternateKey(p => new { p.TenantId, p.Id });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
