using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SolarUY.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRecoleccionPeriodica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ejecutar con el recolector detenido. Las comprobaciones preceden todo DDL.
            migrationBuilder.Sql("""
                BEGIN NOT ATOMIC
                    IF EXISTS (SELECT 1 FROM LecturasPanel GROUP BY PanelId, FechaHora HAVING COUNT(*) > 1) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'req010: existen lecturas duplicadas por panel/instante; resolver sin borrar datos antes de migrar';
                    END IF;
                END
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "NodoId",
                table: "LecturasPanel",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            // No reconstruir procedencia desde Paneles.NodoId: no es una auditoría.
            // Las filas anteriores conservan NodoId NULL y todos sus valores originales.
            // La variable histórica de sesión ya no autoriza atribuciones masivas.

            migrationBuilder.CreateIndex(
                name: "IX_LecturasPanel_NodoId_FechaHora",
                table: "LecturasPanel",
                columns: new[] { "NodoId", "FechaHora" });

            migrationBuilder.CreateIndex(
                name: "IX_LecturasPanel_PanelId_FechaHora",
                table: "LecturasPanel",
                columns: new[] { "PanelId", "FechaHora" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LecturasPanel_Nodos_NodoId",
                table: "LecturasPanel",
                column: "NodoId",
                principalTable: "Nodos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LecturasPanel_Nodos_NodoId",
                table: "LecturasPanel");

            migrationBuilder.DropIndex(
                name: "IX_LecturasPanel_NodoId_FechaHora",
                table: "LecturasPanel");

            migrationBuilder.DropIndex(
                name: "IX_LecturasPanel_PanelId_FechaHora",
                table: "LecturasPanel");

            migrationBuilder.DropColumn(
                name: "NodoId",
                table: "LecturasPanel");

        }
    }
}
