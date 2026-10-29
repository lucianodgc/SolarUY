using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SolarUY.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHistoricalNodeCoverage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_LecturasPanel_PanelId_FechaHora_NodoId_PotenciaGeneradaW",
                table: "LecturasPanel",
                columns: new[] { "PanelId", "FechaHora", "NodoId", "PotenciaGeneradaW" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LecturasPanel_PanelId_FechaHora_NodoId_PotenciaGeneradaW",
                table: "LecturasPanel");
        }
    }
}
