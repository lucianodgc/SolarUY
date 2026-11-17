using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SolarUY.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEstadoSaludAlertas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlertasPanel",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PanelId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Condicion = table.Column<int>(type: "int", nullable: false),
                    DeteccionUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CierreUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    PanelActivo = table.Column<string>(type: "varchar(36)", unicode: false, maxLength: 36, nullable: true, computedColumnSql: "CASE WHEN `CierreUtc` IS NULL THEN RTRIM(`PanelId`) ELSE NULL END", stored: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertasPanel", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertasPanel_Paneles_PanelId",
                        column: x => x.PanelId,
                        principalTable: "Paneles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CambiosEstadoSaludPanel",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PanelId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    EstadoAnterior = table.Column<int>(type: "int", nullable: true),
                    EstadoNuevo = table.Column<int>(type: "int", nullable: false),
                    FechaUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CambiosEstadoSaludPanel", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CambiosEstadoSaludPanel_Paneles_PanelId",
                        column: x => x.PanelId,
                        principalTable: "Paneles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "SaludPaneles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    PanelId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    NodoEvaluadoId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Estado = table.Column<int>(type: "int", nullable: true),
                    Suficiente = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Motivo = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PrimeraObservacionUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EvaluadoUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EvidenciaUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaludPaneles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SaludPaneles_Paneles_PanelId",
                        column: x => x.PanelId,
                        principalTable: "Paneles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AlertasPanel_PanelActivo",
                table: "AlertasPanel",
                column: "PanelActivo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AlertasPanel_PanelId_CierreUtc",
                table: "AlertasPanel",
                columns: new[] { "PanelId", "CierreUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CambiosEstadoSaludPanel_PanelId_FechaUtc_Version",
                table: "CambiosEstadoSaludPanel",
                columns: new[] { "PanelId", "FechaUtc", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_CambiosEstadoSaludPanel_PanelId_Version",
                table: "CambiosEstadoSaludPanel",
                columns: new[] { "PanelId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaludPaneles_PanelId",
                table: "SaludPaneles",
                column: "PanelId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertasPanel");

            migrationBuilder.DropTable(
                name: "CambiosEstadoSaludPanel");

            migrationBuilder.DropTable(
                name: "SaludPaneles");
        }
    }
}
