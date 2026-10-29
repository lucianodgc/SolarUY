using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SolarUY.Infrastructure.Migrations;

[DbContext(typeof(Persistence.ApplicationDbContext))]
[Migration("20261028235200_AddInversorNodo")]
public partial class AddInversorNodo : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Inversores",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                NodoId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                Marca = table.Column<string>(type: "longtext", nullable: false),
                Modelo = table.Column<string>(type: "longtext", nullable: false),
                PotenciaNominalKW = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                Estado = table.Column<int>(type: "int", nullable: false),
                VolcadoRedActivo = table.Column<bool>(type: "tinyint(1)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Inversores", x => x.Id);
                table.ForeignKey(
                    name: "FK_Inversores_Nodos_NodoId",
                    column: x => x.NodoId,
                    principalTable: "Nodos",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_Inversores_NodoId",
            table: "Inversores",
            column: "NodoId",
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "Inversores");
}
