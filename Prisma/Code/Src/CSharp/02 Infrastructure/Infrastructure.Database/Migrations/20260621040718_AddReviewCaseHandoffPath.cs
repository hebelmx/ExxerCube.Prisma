using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExxerCube.Prisma.Infrastructure.Database.Migrations
{
    /// <summary>
    /// G-C2b: Adds <c>HandoffPath</c> (nullable nvarchar(500)) to the <c>ReviewCases</c> table.
    /// The column stores the storage-relative path of the fused expediente handoff artifact so
    /// the reviewer-approval handler can reload the expediente after a human approves the case.
    /// Chains after <c>20260621100000_AuditLedgerAndDdlTrigger</c>.
    /// DO NOT apply to live SQL (DESKTOP-FB2ES22\SQL2025) without owner approval.
    /// </summary>
    public partial class AddReviewCaseHandoffPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HandoffPath",
                table: "ReviewCases",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HandoffPath",
                table: "ReviewCases");
        }
    }
}
