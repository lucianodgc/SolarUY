using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SolarUY.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLecturaPanel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LecturasPanel",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PanelId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FechaHora = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    PotenciaGeneradaW = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    EstadoSalud = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LecturasPanel", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LecturasPanel_Paneles_PanelId",
                        column: x => x.PanelId,
                        principalTable: "Paneles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_LecturasPanel_PanelId_FechaHora_PotenciaGeneradaW",
                table: "LecturasPanel",
                columns: new[] { "PanelId", "FechaHora", "PotenciaGeneradaW" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LecturasPanel");
        }
    }
}
