using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExxerCube.Prisma.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class SyncModelChanges : Migration
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

            migrationBuilder.CreateTable(
                name: "Persona",
                columns: table => new
                {
                    ParteId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Caracter = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PersonaTipo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Paterno = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Materno = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Rfc = table.Column<string>(type: "nvarchar(13)", maxLength: 13, nullable: true),
                    Relacion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Domicilio = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Complementarios = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RfcVariants = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Persona", x => x.ParteId);
                });

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
                    OverriddenClassification = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FileId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewReason = table.Column<int>(type: "int", nullable: false)
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
                name: "IX_AuditRecords_ActionType",
                table: "AuditRecords",
                column: "ActionType");

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_CorrelationId",
                table: "AuditRecords",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_FileId",
                table: "AuditRecords",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_FileId_Timestamp",
                table: "AuditRecords",
                columns: new[] { "FileId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_Timestamp",
                table: "AuditRecords",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditRecords_UserId",
                table: "AuditRecords",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Persona_Rfc",
                table: "Persona",
                column: "Rfc");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCases_AssignedTo",
                table: "ReviewCases",
                column: "AssignedTo");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCases_CreatedAt",
                table: "ReviewCases",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCases_FileId",
                table: "ReviewCases",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCases_Status",
                table: "ReviewCases",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewDecisions_CaseId",
                table: "ReviewDecisions",
                column: "CaseId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewDecisions_ReviewedAt",
                table: "ReviewDecisions",
                column: "ReviewedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewDecisions_ReviewerId",
                table: "ReviewDecisions",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_SLAStatus_Deadline",
                table: "SLAStatus",
                column: "Deadline");

            migrationBuilder.CreateIndex(
                name: "IX_SLAStatus_EscalationLevel",
                table: "SLAStatus",
                column: "EscalationLevel");

            migrationBuilder.CreateIndex(
                name: "IX_SLAStatus_IsAtRisk",
                table: "SLAStatus",
                column: "IsAtRisk");

            migrationBuilder.CreateIndex(
                name: "IX_SLAStatus_IsBreached",
                table: "SLAStatus",
                column: "IsBreached");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditRecords");

            migrationBuilder.DropTable(
                name: "Persona");

            migrationBuilder.DropTable(
                name: "ReviewDecisions");

            migrationBuilder.DropTable(
                name: "SLAStatus");

            migrationBuilder.DropTable(
                name: "ReviewCases");
        }
    }
}
