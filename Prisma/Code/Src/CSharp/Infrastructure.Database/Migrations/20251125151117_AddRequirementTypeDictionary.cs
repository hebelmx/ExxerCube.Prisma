using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ExxerCube.Prisma.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddRequirementTypeDictionary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RequirementTypeDictionary",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DiscoveredAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    DiscoveredFromDocument = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    KeywordPattern = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequirementTypeDictionary", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "RequirementTypeDictionary",
                columns: new[] { "Id", "CreatedBy", "DiscoveredAt", "DiscoveredFromDocument", "DisplayName", "IsActive", "KeywordPattern", "Name", "Notes" },
                values: new object[,]
                {
                    { 100, "System", new DateTime(2025, 1, 25, 0, 0, 0, 0, DateTimeKind.Utc), null, "Solicitud de Información", true, "solicita información|estados de cuenta", "Judicial", "Art. 142 LIC - Judicial/Fiscal/Administrative information requests" },
                    { 101, "System", new DateTime(2025, 1, 25, 0, 0, 0, 0, DateTimeKind.Utc), null, "Aseguramiento/Bloqueo", true, "asegurar|bloquear|embargar", "Aseguramiento", "Art. 2(V)(b) - SAME DAY execution required" },
                    { 102, "System", new DateTime(2025, 1, 25, 0, 0, 0, 0, DateTimeKind.Utc), null, "Desbloqueo", true, "desbloquear|liberar", "Desbloqueo", "R29 Type 102 - Release of frozen funds" },
                    { 103, "System", new DateTime(2025, 1, 25, 0, 0, 0, 0, DateTimeKind.Utc), null, "Transferencia Electrónica", true, "transferir.*CLABE|CLABE.*transferir", "Transferencia", "R29 Type 103 - Electronic transfer to government account" },
                    { 104, "System", new DateTime(2025, 1, 25, 0, 0, 0, 0, DateTimeKind.Utc), null, "Situación de Fondos", true, "cheque de caja|poner a disposición", "SituacionFondos", "R29 Type 104 - Cashier's check to judicial authority" },
                    { 999, "System", new DateTime(2025, 1, 25, 0, 0, 0, 0, DateTimeKind.Utc), null, "Desconocido", true, null, "Unknown", "Fallback for unrecognized requirement types - triggers manual review" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_RequirementTypeDictionary_IsActive",
                table: "RequirementTypeDictionary",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RequirementTypeDictionary");
        }
    }
}
