using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MultiTenantSaaS.Domain.Entities;

namespace MultiTenantSaaS.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.Email).IsRequired().HasMaxLength(320);
        builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(500);
        builder.Property(u => u.FullName).IsRequired().HasMaxLength(200);
        builder.Property(u => u.Role).HasConversion<int>().HasColumnName("RoleId");

        // Email uniqueness is per tenant, not global: the same person may hold accounts at
        // several client organizations.
        builder.HasIndex(u => new { u.TenantId, u.Email }).IsUnique();

        builder.HasOne(u => u.Tenant)
            .WithMany()
            .HasForeignKey(u => u.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Foreign key to the lookup table, without a navigation property.
        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(u => u.Role)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
