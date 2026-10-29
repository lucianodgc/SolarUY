using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SolarUY.Domain.Entities;

namespace SolarUY.Infrastructure.Persistence;

public sealed class InversorConfiguration : IEntityTypeConfiguration<Inversor>
{
    public void Configure(EntityTypeBuilder<Inversor> builder)
    {
        builder.ToTable("Inversores");
        builder.HasKey(inversor => inversor.Id);
        builder.Property(inversor => inversor.Marca).IsRequired();
        builder.Property(inversor => inversor.Modelo).IsRequired();
        builder.Property(inversor => inversor.PotenciaNominalKW).IsRequired();
        builder.HasIndex(inversor => inversor.NodoId).IsUnique();
        builder.HasOne(inversor => inversor.Nodo).WithOne(nodo => nodo.Inversor)
            .HasForeignKey<Inversor>(inversor => inversor.NodoId).OnDelete(DeleteBehavior.Restrict);
    }
}
