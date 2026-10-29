using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SolarUY.Infrastructure.Persistence;

namespace SolarUY.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261029120000_AddDistribucionMonitoreo")]
public sealed partial class AddDistribucionMonitoreo : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE SecuenciaMonitoreo (Id INT NOT NULL PRIMARY KEY, Valor BIGINT NOT NULL) ENGINE=InnoDB;
            INSERT INTO SecuenciaMonitoreo (Id, Valor) VALUES (1, 0);
            CREATE TABLE AvisosMonitoreo (Secuencia BIGINT NOT NULL PRIMARY KEY, Contenido LONGTEXT NOT NULL) ENGINE=InnoDB;
            CREATE TABLE CursoresMonitoreo (InstanciaId VARCHAR(100) NOT NULL PRIMARY KEY, Secuencia BIGINT NOT NULL) ENGINE=InnoDB;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TABLE CursoresMonitoreo; DROP TABLE AvisosMonitoreo; DROP TABLE SecuenciaMonitoreo;");
    }
}
