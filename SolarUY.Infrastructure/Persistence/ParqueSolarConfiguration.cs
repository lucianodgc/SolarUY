using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SolarUY.Domain.Entities;

namespace SolarUY.Infrastructure.Persistence;

public sealed class ParqueSolarConfiguration : IEntityTypeConfiguration<ParqueSolar>
{
    public void Configure(EntityTypeBuilder<ParqueSolar> builder)
    {
        builder.ToTable("ParquesSolares");
        builder.HasKey(parque => parque.Id);
        builder.Property(parque => parque.Nombre).IsRequired().HasMaxLength(100);
        builder.Property(parque => parque.Ubicacion).IsRequired().HasMaxLength(200);
        builder.HasIndex(parque => parque.Nombre).IsUnique();
    }
}
