using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SolarUY.Domain.Entities;

namespace SolarUY.Infrastructure.Persistence;

public sealed class TareaMantenimientoConfiguration : IEntityTypeConfiguration<TareaMantenimiento>
{
    public void Configure(EntityTypeBuilder<TareaMantenimiento> builder)
    {
        builder.ToTable("TareasMantenimiento");
        builder.HasKey(tarea => tarea.Id);
        builder.Property(tarea => tarea.Tipo).IsRequired();
        builder.Property(tarea => tarea.Descripcion).IsRequired();
        builder.Property(tarea => tarea.ResponsableId).IsRequired();
        builder.Property(tarea => tarea.FechaProgramada)
            .HasConversion(value => value, value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
        builder.Property(tarea => tarea.FechaRealizacion)
            .HasConversion(value => value, value => value.HasValue
                ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : (DateTime?)null);
        builder.HasOne(tarea => tarea.Panel).WithMany().HasForeignKey(tarea => tarea.PanelId).OnDelete(DeleteBehavior.Restrict);
    }
}
