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

        // Indexes are prefixed with TenantId because the query filter always adds
        // TenantId = @p to the WHERE clause. An index on Status alone would be unusable.
        builder.HasIndex(t => new { t.TenantId, t.ProjectId });
        builder.HasIndex(t => new { t.TenantId, t.Status });
        builder.HasIndex(t => new { t.TenantId, t.AssignedToUserId });

        // Supports the default listing order (newest first) without an in-memory sort.
        builder.HasIndex(t => new { t.TenantId, t.CreatedAtUtc });

        // Composite foreign key to Projects (TenantId, Id): PostgreSQL itself rejects a ticket
        // that references another tenant's project.
        builder.HasOne(t => t.Project)
            .WithMany()
            .HasForeignKey(t => new { t.TenantId, t.ProjectId })
            .HasPrincipalKey(p => new { p.TenantId, p.Id })
            .OnDelete(DeleteBehavior.Cascade);
    }
}
