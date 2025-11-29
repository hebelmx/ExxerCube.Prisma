using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExxerCube.Prisma.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditRecordsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditRecords",
                columns: table => new
                {
                    AuditId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    ActionDetails = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Stage = table.Column<int>(type: "int", nullable: false),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditRecords", x => x.AuditId);
                    table.ForeignKey(
                        name: "FK_AuditRecords_FileMetadata_FileId",
                        column: x => x.FileId,
                        principalTable: "FileMetadata",
                        principalColumn: "FileId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_FileId",
                table: "AuditRecords",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_Timestamp",
                table: "AuditRecords",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_ActionType",
                table: "AuditRecords",
                column: "ActionType");

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_UserId",
                table: "AuditRecords",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_CorrelationId",
                table: "AuditRecords",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_FileId_Timestamp",
                table: "AuditRecords",
                columns: new[] { "FileId", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditRecords");
        }
    }
}

