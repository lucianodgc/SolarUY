using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SolarUY.Domain.Entities;

namespace SolarUY.Infrastructure.Persistence;

public sealed class PanelConfiguration : IEntityTypeConfiguration<Panel>
{
    public void Configure(EntityTypeBuilder<Panel> builder)
    {
        builder.ToTable("Paneles");
        builder.HasKey(panel => panel.Id);
        builder.Property(panel => panel.NumeroSerie).IsRequired();
        builder.HasIndex(panel => panel.NumeroSerie).IsUnique();
        builder.Property(panel => panel.Marca).IsRequired();
        builder.Property(panel => panel.Modelo).IsRequired();
        builder.Property(panel => panel.FechaInstalacion)
            .HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
        builder.HasOne(panel => panel.Nodo).WithMany().HasForeignKey(panel => panel.NodoId).OnDelete(DeleteBehavior.Restrict);
    }
}
