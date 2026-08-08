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

        // Fără asta EF ar trata cheia întreagă drept identity și ar ignora valorile din seed.
        builder.Property(r => r.Id).HasConversion<int>().ValueGeneratedNever();

        builder.Property(r => r.Name).IsRequired().HasMaxLength(50);
        builder.Property(r => r.Description).IsRequired().HasMaxLength(200);

        builder.HasData(
            new { Id = UserRole.GlobalAdmin, Name = "GlobalAdmin", Description = "Administrator de platformă." },
            new { Id = UserRole.TenantAdmin, Name = "TenantAdmin", Description = "Administrator al organizației." },
            new { Id = UserRole.Member, Name = "Member", Description = "Utilizator al organizației." });
    }
}
