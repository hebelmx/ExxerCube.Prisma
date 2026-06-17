using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialVeriqanSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "veriqan");

            migrationBuilder.CreateTable(
                name: "VerificationJobs",
                schema: "veriqan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerificationJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Findings",
                schema: "veriqan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VerificationJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CheckId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Verdict = table.Column<int>(type: "int", nullable: false),
                    Expected = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Observed = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    EngineVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Findings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Findings_VerificationJobs_VerificationJobId",
                        column: x => x.VerificationJobId,
                        principalSchema: "veriqan",
                        principalTable: "VerificationJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "JobVerdicts",
                schema: "veriqan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VerificationJobId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Signal = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobVerdicts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobVerdicts_VerificationJobs_VerificationJobId",
                        column: x => x.VerificationJobId,
                        principalSchema: "veriqan",
                        principalTable: "VerificationJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Findings_CheckId",
                schema: "veriqan",
                table: "Findings",
                column: "CheckId");

            migrationBuilder.CreateIndex(
                name: "IX_Findings_VerificationJobId",
                schema: "veriqan",
                table: "Findings",
                column: "VerificationJobId");

            migrationBuilder.CreateIndex(
                name: "IX_JobVerdicts_VerificationJobId",
                schema: "veriqan",
                table: "JobVerdicts",
                column: "VerificationJobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VerificationJobs_ContentHash",
                schema: "veriqan",
                table: "VerificationJobs",
                column: "ContentHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VerificationJobs_ReceivedAtUtc",
                schema: "veriqan",
                table: "VerificationJobs",
                column: "ReceivedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_VerificationJobs_Status",
                schema: "veriqan",
                table: "VerificationJobs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Findings",
                schema: "veriqan");

            migrationBuilder.DropTable(
                name: "JobVerdicts",
                schema: "veriqan");

            migrationBuilder.DropTable(
                name: "VerificationJobs",
                schema: "veriqan");
        }
    }
}
