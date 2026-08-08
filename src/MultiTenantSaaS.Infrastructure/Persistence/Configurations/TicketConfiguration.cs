using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MultiTenantSaaS.Domain.Entities;

namespace MultiTenantSaaS.Infrastructure.Persistence.Configurations;

public sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Title).IsRequired().HasMaxLength(300);
        builder.Property(t => t.Description).HasMaxLength(5000);
        builder.Property(t => t.Status).HasConversion<int>();
        builder.Property(t => t.Priority).HasConversion<int>();

        // Indexuri prefixate cu TenantId, fiindcă query filter-ul adaugă întotdeauna
        // TenantId = @p în WHERE. Un index doar pe Status ar fi inutilizabil aici.
        builder.HasIndex(t => new { t.TenantId, t.ProjectId });
        builder.HasIndex(t => new { t.TenantId, t.Status });
        builder.HasIndex(t => new { t.TenantId, t.AssignedToUserId });

        // Cheie străină compusă către (TenantId, Id) din Projects: PostgreSQL însuși
        // respinge un tichet care referă proiectul altui tenant.
        builder.HasOne(t => t.Project)
            .WithMany()
            .HasForeignKey(t => new { t.TenantId, t.ProjectId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
