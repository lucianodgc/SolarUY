using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SolarUY.Domain.Entities;

namespace SolarUY.Infrastructure.Persistence;

public sealed class NodoConfiguration : IEntityTypeConfiguration<Nodo>
{
    public void Configure(EntityTypeBuilder<Nodo> builder)
    {
        builder.ToTable("Nodos");
        builder.HasKey(nodo => nodo.Id);
        builder.Property(nodo => nodo.Nombre).IsRequired();
        builder.Property(nodo => nodo.UbicacionEnParque).IsRequired();
        builder.HasOne(nodo => nodo.Parque).WithMany().HasForeignKey(nodo => nodo.ParqueId).OnDelete(DeleteBehavior.Restrict);
    }
}
