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
            .HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
        builder.HasIndex(lectura => new { lectura.PanelId, lectura.FechaHora, lectura.PotenciaGeneradaW });
        builder.HasOne(lectura => lectura.Panel).WithMany().HasForeignKey(lectura => lectura.PanelId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
