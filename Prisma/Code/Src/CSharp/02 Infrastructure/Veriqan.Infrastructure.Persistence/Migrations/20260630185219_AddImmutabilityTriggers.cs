using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Adds database-level immutability triggers for the append-only audit tables
    /// (<c>veriqan.Dispositions</c> and <c>veriqan.ReprocessAuditLog</c>).
    /// UPDATE and DELETE on either table raise error 51000 and are rolled back immediately.
    /// <c>veriqan.VerificationOutcomeSnapshots</c> is intentionally NOT protected —
    /// <c>ReplaceOutcomeAsync</c> legitimately UPDATEs that table on reprocess.
    /// </summary>
    /// <remarks>
    /// Story 6.2 — DB-enforced audit immutability (defense-in-depth, AR-9 / FR-18).
    /// The application-layer <c>ImmutableEntityInterceptor</c> provides a fast early rejection;
    /// these triggers are the enforcement guarantee at the DB engine level.
    /// <para>
    /// <b>Enforcement boundary (deployment contract):</b>
    /// <c>AFTER UPDATE, DELETE</c> triggers do <b>not</b> fire on <c>TRUNCATE TABLE</c>.
    /// Additionally, a DB principal granted <c>ALTER TABLE</c>, <c>CONTROL</c>, or membership
    /// in <c>db_owner</c> can execute <c>DISABLE TRIGGER</c>, bypassing the triggers entirely.
    /// Therefore the engine-level tamper-evidence provided by these triggers holds <b>only</b>
    /// under a least-privilege deployment where the application DB principal is granted
    /// <c>INSERT</c> and <c>SELECT</c> on <c>veriqan.Dispositions</c> and
    /// <c>veriqan.ReprocessAuditLog</c>, and is explicitly <b>NOT</b> granted
    /// <c>ALTER TABLE</c>, <c>CONTROL</c>, or <c>db_owner</c> on the audit tables.
    /// Operational scripts that must truncate these tables for DR or restore purposes must
    /// be run under a privileged DBA role — never under the application principal.
    /// </para>
    /// </remarks>
    public partial class AddImmutabilityTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [veriqan].[trg_Dispositions_PreventMutation]
                ON [veriqan].[Dispositions]
                AFTER UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51000, N'Dispositions are immutable: UPDATE and DELETE are prohibited.', 1;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER TRIGGER [veriqan].[trg_ReprocessAuditLog_PreventMutation]
                ON [veriqan].[ReprocessAuditLog]
                AFTER UPDATE, DELETE
                AS
                BEGIN
                    SET NOCOUNT ON;
                    THROW 51000, N'ReprocessAuditLog are immutable: UPDATE and DELETE are prohibited.', 1;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS [veriqan].[trg_Dispositions_PreventMutation];");

            migrationBuilder.Sql(
                "DROP TRIGGER IF EXISTS [veriqan].[trg_ReprocessAuditLog_PreventMutation];");
        }
    }
}

