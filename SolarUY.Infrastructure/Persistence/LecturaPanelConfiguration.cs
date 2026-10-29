using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SolarUY.Domain.Entities;

namespace SolarUY.Infrastructure.Persistence;

public sealed class LecturaPanelConfiguration : IEntityTypeConfiguration<LecturaPanel>
{
    public void Configure(EntityTypeBuilder<LecturaPanel> builder)
    {
        builder.ToTable("LecturasPanel");
        builder.HasKey(lectura => lectura.Id);
        builder.Property(lectura => lectura.PotenciaGeneradaW).HasPrecision(18, 6);
        builder.Property(lectura => lectura.FechaHora)
            .HasColumnType("datetime(6)")
            .HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
        builder.HasIndex(lectura => new { lectura.PanelId, lectura.FechaHora }).IsUnique();
        // Conservar el índice de cobertura utilizado por las consultas históricas existentes.
        builder.HasIndex(lectura => new { lectura.PanelId, lectura.FechaHora, lectura.PotenciaGeneradaW });
        builder.HasIndex(lectura => new { lectura.NodoId, lectura.FechaHora });
        builder.HasOne(lectura => lectura.Nodo).WithMany().HasForeignKey(lectura => lectura.NodoId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(lectura => lectura.Panel).WithMany().HasForeignKey(lectura => lectura.PanelId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
