using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExxerCube.Prisma.Infrastructure.Database.Migrations
{
    /// <inheritdoc />
    public partial class DropAuditFileMetadataFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the foreign key FK_AuditRecords_FileMetadata_FileId.
            // Worker processes emit audit records (non-null FileId) before the FileMetadata row exists;
            // the FK caused those INSERTs to fail and the records to be silently dropped (fail-open).
            // Owner decision: FileId stays as a plain indexed column — no referential constraint.
            migrationBuilder.DropForeignKey(
                name: "FK_AuditRecords_FileMetadata_FileId",
                table: "AuditRecords");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the foreign key — only safe to apply on a database where all AuditRecord.FileId
            // values reference existing FileMetadata rows (may require data cleanup before running Down).
            migrationBuilder.AddForeignKey(
                name: "FK_AuditRecords_FileMetadata_FileId",
                table: "AuditRecords",
                column: "FileId",
                principalTable: "FileMetadata",
                principalColumn: "FileId",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
