using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SolarUY.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PreserveUnknownHistoricalNodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "NodoId",
                table: "LecturasPanel",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)")
                .OldAnnotation("Relational:Collation", "ascii_general_ci");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                BEGIN NOT ATOMIC
                    IF EXISTS (SELECT 1 FROM LecturasPanel WHERE NodoId IS NULL) THEN
                        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'No se puede revertir: existen lecturas sin nodo historico verificado';
                    END IF;
                END
                """);
            migrationBuilder.AlterColumn<Guid>(
                name: "NodoId",
                table: "LecturasPanel",
                type: "char(36)",
                nullable: false,
                collation: "ascii_general_ci",
                oldClrType: typeof(Guid),
                oldType: "char(36)",
                oldNullable: true)
                .OldAnnotation("Relational:Collation", "ascii_general_ci");
        }
    }
}
