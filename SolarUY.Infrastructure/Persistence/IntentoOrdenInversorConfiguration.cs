using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SolarUY.Domain.Entities;

namespace SolarUY.Infrastructure.Persistence;

public sealed class IntentoOrdenInversorConfiguration : IEntityTypeConfiguration<IntentoOrdenInversor>
{
    public void Configure(EntityTypeBuilder<IntentoOrdenInversor> builder)
    {
        builder.ToTable("IntentosOrdenInversor");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ActorSub).HasMaxLength(255).UseCollation("utf8mb4_bin");
        builder.Property(x => x.NodoSolicitado).HasMaxLength(128);
        builder.Property(x => x.PanelSolicitado).HasMaxLength(128);
        builder.Property(x => x.Codigo).HasMaxLength(64);
        builder.Property(x => x.DestinoNombre).HasMaxLength(300);
        builder.Property(x => x.Accion).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Estado).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.SolicitadaUtc).HasColumnType("datetime(6)")
            .HasConversion(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        builder.Property(x => x.ResultadoUtc).HasColumnType("datetime(6)")
            .HasConversion(v => v, v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);
        builder.HasIndex(x => new { x.ActorSub, x.Correlacion }).IsUnique();
        builder.HasIndex(x => new { x.Estado, x.SolicitadaUtc });
        // Identificadores históricos sin FK: borrar inventario no borra la auditoría.
    }
}
