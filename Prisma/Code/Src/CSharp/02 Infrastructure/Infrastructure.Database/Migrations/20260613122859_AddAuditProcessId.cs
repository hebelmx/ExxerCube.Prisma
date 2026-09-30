using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExxerCube.Prisma.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditProcessId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProcessId",
                table: "AuditRecords",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_ProcessId",
                table: "AuditRecords",
                column: "ProcessId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditRecords_ProcessId",
                table: "AuditRecords");

            migrationBuilder.DropColumn(
                name: "ProcessId",
                table: "AuditRecords");
        }
    }
}
