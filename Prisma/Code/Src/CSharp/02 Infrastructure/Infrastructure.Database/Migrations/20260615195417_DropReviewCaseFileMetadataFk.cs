using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExxerCube.Prisma.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class DropReviewCaseFileMetadataFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReviewCases_FileMetadata_FileId",
                table: "ReviewCases");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_ReviewCases_FileMetadata_FileId",
                table: "ReviewCases",
                column: "FileId",
                principalTable: "FileMetadata",
                principalColumn: "FileId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
