using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SolarUY.Infrastructure.Persistence;

namespace SolarUY.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261105010000_RemoveDistribucionMonitoreo")]
public sealed class RemoveDistribucionMonitoreo : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Solo se retira almacenamiento técnico; las mediciones y el inventario se conservan.
        migrationBuilder.Sql("DROP TABLE IF EXISTS CursoresMonitoreo;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS AvisosMonitoreo;");
        migrationBuilder.Sql("DROP TABLE IF EXISTS SecuenciaMonitoreo;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Se restaura el esquema vacío: los avisos y cursores retirados no se recuperan.
        migrationBuilder.Sql("CREATE TABLE SecuenciaMonitoreo (Id INT NOT NULL PRIMARY KEY, Valor BIGINT NOT NULL) ENGINE=InnoDB;");
        migrationBuilder.Sql("INSERT INTO SecuenciaMonitoreo (Id, Valor) VALUES (1, 0);");
        migrationBuilder.Sql("CREATE TABLE AvisosMonitoreo (Secuencia BIGINT NOT NULL PRIMARY KEY, Contenido LONGTEXT NOT NULL) ENGINE=InnoDB;");
        migrationBuilder.Sql("CREATE TABLE CursoresMonitoreo (InstanciaId VARCHAR(100) NOT NULL PRIMARY KEY, Secuencia BIGINT NOT NULL) ENGINE=InnoDB;");
    }
}
