using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExxerCube.Prisma.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddSLAStatusTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SLAStatus",
                columns: table => new
                {
                    FileId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IntakeDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Deadline = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DaysPlazo = table.Column<int>(type: "int", nullable: false),
                    RemainingTime = table.Column<long>(type: "bigint", nullable: false),
                    IsAtRisk = table.Column<bool>(type: "bit", nullable: false),
                    IsBreached = table.Column<bool>(type: "bit", nullable: false),
                    EscalationLevel = table.Column<int>(type: "int", nullable: false),
                    EscalatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SLAStatus", x => x.FileId);
                    table.ForeignKey(
                        name: "FK_SLAStatus_FileMetadata_FileId",
                        column: x => x.FileId,
                        principalTable: "FileMetadata",
                        principalColumn: "FileId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SLAStatus_Deadline",
                table: "SLAStatus",
                column: "Deadline");

            migrationBuilder.CreateIndex(
                name: "IX_SLAStatus_IsAtRisk",
                table: "SLAStatus",
                column: "IsAtRisk");

            migrationBuilder.CreateIndex(
                name: "IX_SLAStatus_IsBreached",
                table: "SLAStatus",
                column: "IsBreached");

            migrationBuilder.CreateIndex(
                name: "IX_SLAStatus_EscalationLevel",
                table: "SLAStatus",
                column: "EscalationLevel");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SLAStatus");
        }
    }
}

