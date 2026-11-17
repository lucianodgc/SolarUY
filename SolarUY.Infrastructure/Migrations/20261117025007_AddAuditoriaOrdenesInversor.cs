using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SolarUY.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditoriaOrdenesInversor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntentosOrdenInversor",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Correlacion = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ActorSub = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true, collation: "utf8mb4_bin")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Autenticado = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Administrador = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Accion = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NodoSolicitado = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PanelSolicitado = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    NodoValidado = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    PanelValidado = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    DestinoNombre = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SolicitadaUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ResultadoUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Estado = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Codigo = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntentosOrdenInversor", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_IntentosOrdenInversor_ActorSub_Correlacion",
                table: "IntentosOrdenInversor",
                columns: new[] { "ActorSub", "Correlacion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IntentosOrdenInversor_Estado_SolicitadaUtc",
                table: "IntentosOrdenInversor",
                columns: new[] { "Estado", "SolicitadaUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntentosOrdenInversor");
        }
    }
}
