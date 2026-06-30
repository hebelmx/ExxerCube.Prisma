using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDurableStores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReprocessAuditLog",
                schema: "veriqan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BeforeSignal = table.Column<int>(type: "int", nullable: true),
                    AfterSignal = table.Column<int>(type: "int", nullable: false),
                    ReprocessedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReprocessAuditLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VerificationOutcomeSnapshots",
                schema: "veriqan",
                columns: table => new
                {
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    OutcomeJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SavedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReplacedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificationOutcomeSnapshots", x => x.ContentHash);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReprocessAuditLog_ContentHash",
                schema: "veriqan",
                table: "ReprocessAuditLog",
                columns: new[] { "ContentHash", "ReprocessedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReprocessAuditLog",
                schema: "veriqan");

            migrationBuilder.DropTable(
                name: "VerificationOutcomeSnapshots",
                schema: "veriqan");
        }
    }
}
