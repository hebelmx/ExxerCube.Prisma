using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExxerCube.Prisma.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class RenameRequirementTypeJudicialToInformationRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "RequirementTypeDictionary",
                keyColumn: "Id",
                keyValue: 100,
                column: "Name",
                value: "InformationRequest");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "RequirementTypeDictionary",
                keyColumn: "Id",
                keyValue: 100,
                column: "Name",
                value: "Judicial");
        }
    }
}
