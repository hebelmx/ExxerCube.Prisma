using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDispositionAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Dispositions",
                schema: "veriqan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VerificationJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FindingId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Action = table.Column<int>(type: "int", nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DispositionedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    BeforeState = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AfterState = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EngineVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReferenceBundleVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dispositions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Dispositions_FindingId",
                schema: "veriqan",
                table: "Dispositions",
                column: "FindingId");

            migrationBuilder.CreateIndex(
                name: "IX_Dispositions_VerificationJobId",
                schema: "veriqan",
                table: "Dispositions",
                column: "VerificationJobId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Dispositions",
                schema: "veriqan");
        }
    }
}
