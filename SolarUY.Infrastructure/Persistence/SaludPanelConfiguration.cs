using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SolarUY.Domain.Salud;

namespace SolarUY.Infrastructure.Persistence;

public sealed class SaludPanelConfiguration : IEntityTypeConfiguration<SaludPanel>
{
    public void Configure(EntityTypeBuilder<SaludPanel> b)
    {
        b.ToTable("SaludPaneles"); b.HasKey(x => x.Id); b.HasIndex(x => x.PanelId).IsUnique();
        b.Property(x => x.Motivo).HasMaxLength(500).IsRequired();
        b.Property(x => x.Version).IsConcurrencyToken();
        b.Property(x => x.Estado).HasConversion<int>();
        b.HasOne(x => x.Panel).WithMany().HasForeignKey(x => x.PanelId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.PrimeraObservacionUtc).HasColumnType("datetime(6)").HasConversion(x => x, x => DateTime.SpecifyKind(x, DateTimeKind.Utc));
        b.Property(x => x.EvaluadoUtc).HasColumnType("datetime(6)").HasConversion(x => x, x => DateTime.SpecifyKind(x, DateTimeKind.Utc));
        b.Property(x => x.EvidenciaUtc).HasColumnType("datetime(6)").HasConversion(new ValueConverter<DateTime, DateTime>(x => x, x => DateTime.SpecifyKind(x, DateTimeKind.Utc)));
    }
}
public sealed class CambioEstadoSaludPanelConfiguration : IEntityTypeConfiguration<CambioEstadoSaludPanel>
{
    public void Configure(EntityTypeBuilder<CambioEstadoSaludPanel> b)
    {
        b.ToTable("CambiosEstadoSaludPanel"); b.HasKey(x => x.Id);
        b.Property(x => x.EstadoAnterior).HasConversion<int>();
        b.Property(x => x.EstadoNuevo).HasConversion<int>();
        b.HasIndex(x => new { x.PanelId, x.Version }).IsUnique();
        b.HasIndex(x => new { x.PanelId, x.FechaUtc, x.Version });
        b.HasOne(x => x.Panel).WithMany().HasForeignKey(x => x.PanelId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.FechaUtc).HasColumnType("datetime(6)").HasConversion(x => x, x => DateTime.SpecifyKind(x, DateTimeKind.Utc));
    }
}
public sealed class AlertaPanelConfiguration : IEntityTypeConfiguration<AlertaPanel>
{
    public void Configure(EntityTypeBuilder<AlertaPanel> b)
    {
        b.ToTable("AlertasPanel"); b.HasKey(x => x.Id);
        b.Property(x => x.Condicion).HasConversion<int>();
        b.Property<string>("PanelActivo").HasMaxLength(36).IsUnicode(false).UseCollation("ascii_general_ci")
            .HasComputedColumnSql("CASE WHEN `CierreUtc` IS NULL THEN RTRIM(`PanelId`) ELSE NULL END", stored: true);
        b.HasIndex("PanelActivo").IsUnique();
        b.HasIndex(x => new { x.PanelId, x.CierreUtc });
        b.HasOne(x => x.Panel).WithMany().HasForeignKey(x => x.PanelId).OnDelete(DeleteBehavior.Restrict);
        b.Property(x => x.DeteccionUtc).HasColumnType("datetime(6)").HasConversion(x => x, x => DateTime.SpecifyKind(x, DateTimeKind.Utc));
        b.Property(x => x.CierreUtc).HasColumnType("datetime(6)").HasConversion(new ValueConverter<DateTime, DateTime>(x => x, x => DateTime.SpecifyKind(x, DateTimeKind.Utc)));
    }
}
