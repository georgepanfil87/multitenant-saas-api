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

        // Cheie alternativă (TenantId, Id): ținta compusă a cheii străine din Tickets.
        // Fără ea, un tichet ar putea referi, la nivel de bază de date, un proiect al
        // altui tenant - filtrele EF nu se aplică integrității referențiale.
        builder.HasAlternateKey(p => new { p.TenantId, p.Id });

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(p => p.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
