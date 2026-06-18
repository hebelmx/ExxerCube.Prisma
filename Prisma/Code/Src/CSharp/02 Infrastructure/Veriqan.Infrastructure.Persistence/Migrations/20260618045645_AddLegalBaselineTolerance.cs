using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLegalBaselineTolerance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LegalBaselineTolerances",
                schema: "veriqan",
                columns: table => new
                {
                    CheckId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LegalDefault = table.Column<string>(type: "nvarchar(500)", nullable: false),
                    Min = table.Column<string>(type: "nvarchar(500)", nullable: false),
                    Max = table.Column<string>(type: "nvarchar(500)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LegalBaselineTolerances", x => x.CheckId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LegalBaselineTolerances",
                schema: "veriqan");
        }
    }
}
