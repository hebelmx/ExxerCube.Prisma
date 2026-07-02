using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExxerCube.Prisma.Infrastructure.Database.Migrations
{
    /// <summary>
    /// Creates the UnifiedMetadataRecords table (persisted unified metadata store).
    /// </summary>
    /// <remarks>
    /// GH#32: supersedes the orphaned <c>20260613200000_AddUnifiedMetadataRecords</c> migration, which
    /// shipped without a <c>.Designer.cs</c> — so it carried no <c>[Migration]</c> attribute, was invisible
    /// to the migrations assembly, and was never applied (the table was missing everywhere migrations run).
    /// Re-authored as a terminal migration whose target model equals the current model snapshot, so it is
    /// design-time consistent and <c>MigrateAsync</c> creates the table on any DB that lacks it.
    /// </remarks>
    public partial class AddUnifiedMetadataRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UnifiedMetadataRecords",
                columns: table => new
                {
                    FileId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnifiedMetadataRecords", x => x.FileId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UnifiedMetadataRecords_UpdatedAt",
                table: "UnifiedMetadataRecords",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UnifiedMetadataRecords");
        }
    }
}
