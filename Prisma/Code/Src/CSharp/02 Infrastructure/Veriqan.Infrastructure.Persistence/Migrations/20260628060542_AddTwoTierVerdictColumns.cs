using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTwoTierVerdictColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BankTierVerdict",
                schema: "veriqan",
                table: "JobVerdicts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CondusefTierVerdict",
                schema: "veriqan",
                table: "JobVerdicts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Tier",
                schema: "veriqan",
                table: "Findings",
                type: "int",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankTierVerdict",
                schema: "veriqan",
                table: "JobVerdicts");

            migrationBuilder.DropColumn(
                name: "CondusefTierVerdict",
                schema: "veriqan",
                table: "JobVerdicts");

            migrationBuilder.DropColumn(
                name: "Tier",
                schema: "veriqan",
                table: "Findings");
        }
    }
}
