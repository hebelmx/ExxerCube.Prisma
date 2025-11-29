using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExxerCube.Prisma.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddEventInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Channel",
                table: "FileMetadata",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "EvidenceHash",
                table: "FileMetadata",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LinkedExpediente",
                table: "FileMetadata",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LinkedOficio",
                table: "FileMetadata",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SignatureType",
                table: "FileMetadata",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Channel",
                table: "FileMetadata");

            migrationBuilder.DropColumn(
                name: "EvidenceHash",
                table: "FileMetadata");

            migrationBuilder.DropColumn(
                name: "LinkedExpediente",
                table: "FileMetadata");

            migrationBuilder.DropColumn(
                name: "LinkedOficio",
                table: "FileMetadata");

            migrationBuilder.DropColumn(
                name: "SignatureType",
                table: "FileMetadata");
        }
    }
}
