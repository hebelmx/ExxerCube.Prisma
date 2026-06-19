using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobVerdictAlertSentAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Additive nullable column — no data loss; existing rows get NULL (no alert sent).
            // IsConcurrencyToken() is a client-side EF behaviour annotation only; no DDL change needed.
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AlertSentAt",
                schema: "veriqan",
                table: "JobVerdicts",
                type: "datetimeoffset",
                nullable: true,
                defaultValue: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AlertSentAt",
                schema: "veriqan",
                table: "JobVerdicts");
        }
    }
}
