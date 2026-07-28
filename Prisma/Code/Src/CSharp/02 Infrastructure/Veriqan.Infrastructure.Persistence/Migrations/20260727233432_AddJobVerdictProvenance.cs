using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobVerdictProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EngineVersion",
                schema: "veriqan",
                table: "JobVerdicts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "unknown");

            migrationBuilder.AddColumn<string>(
                name: "ReferenceBundleVersion",
                schema: "veriqan",
                table: "JobVerdicts",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EngineVersion",
                schema: "veriqan",
                table: "JobVerdicts");

            migrationBuilder.DropColumn(
                name: "ReferenceBundleVersion",
                schema: "veriqan",
                table: "JobVerdicts");
        }
    }
}
