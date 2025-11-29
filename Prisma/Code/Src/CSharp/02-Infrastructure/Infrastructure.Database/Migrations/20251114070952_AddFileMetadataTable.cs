using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExxerCube.Prisma.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddFileMetadataTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileMetadata",
                columns: table => new
                {
                    FileId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DownloadTimestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Checksum = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    Format = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileMetadata", x => x.FileId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileMetadata_Checksum",
                table: "FileMetadata",
                column: "Checksum");

            migrationBuilder.CreateIndex(
                name: "IX_FileMetadata_DownloadTimestamp",
                table: "FileMetadata",
                column: "DownloadTimestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FileMetadata");
        }
    }
}
