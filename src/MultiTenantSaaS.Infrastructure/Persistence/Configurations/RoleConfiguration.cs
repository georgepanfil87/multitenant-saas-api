using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MultiTenantSaaS.Domain.Entities;
using MultiTenantSaaS.Domain.Enums;

namespace MultiTenantSaaS.Infrastructure.Persistence.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Roles");
        builder.HasKey(r => r.Id);

        // Without this, EF would treat the integer key as an identity column and ignore seeds.
        builder.Property(r => r.Id).HasConversion<int>().ValueGeneratedNever();

        builder.Property(r => r.Name).IsRequired().HasMaxLength(50);
        builder.Property(r => r.Description).IsRequired().HasMaxLength(200);

        builder.HasData(
            new { Id = UserRole.GlobalAdmin, Name = "GlobalAdmin", Description = "Platform administrator." },
            new { Id = UserRole.TenantAdmin, Name = "TenantAdmin", Description = "Organization administrator." },
            new { Id = UserRole.Member, Name = "Member", Description = "Organization user." });
    }
}
