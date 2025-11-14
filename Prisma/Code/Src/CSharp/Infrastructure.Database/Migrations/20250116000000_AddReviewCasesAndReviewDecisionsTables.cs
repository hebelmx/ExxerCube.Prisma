using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExxerCube.Prisma.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddReviewCasesAndReviewDecisionsTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReviewCases",
                columns: table => new
                {
                    CaseId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RequiresReviewReason = table.Column<int>(type: "int", nullable: false),
                    ConfidenceLevel = table.Column<int>(type: "int", nullable: false),
                    ClassificationAmbiguity = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AssignedTo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewCases", x => x.CaseId);
                    table.ForeignKey(
                        name: "FK_ReviewCases_FileMetadata_FileId",
                        column: x => x.FileId,
                        principalTable: "FileMetadata",
                        principalColumn: "FileId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReviewDecisions",
                columns: table => new
                {
                    DecisionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CaseId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DecisionType = table.Column<int>(type: "int", nullable: false),
                    ReviewerId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    OverriddenFields = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    OverriddenClassification = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewDecisions", x => x.DecisionId);
                    table.ForeignKey(
                        name: "FK_ReviewDecisions_ReviewCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "ReviewCases",
                        principalColumn: "CaseId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCases_FileId",
                table: "ReviewCases",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCases_Status",
                table: "ReviewCases",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCases_CreatedAt",
                table: "ReviewCases",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCases_AssignedTo",
                table: "ReviewCases",
                column: "AssignedTo");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewDecisions_CaseId",
                table: "ReviewDecisions",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewDecisions_ReviewerId",
                table: "ReviewDecisions",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewDecisions_ReviewedAt",
                table: "ReviewDecisions",
                column: "ReviewedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReviewDecisions");

            migrationBuilder.DropTable(
                name: "ReviewCases");
        }
    }
}

