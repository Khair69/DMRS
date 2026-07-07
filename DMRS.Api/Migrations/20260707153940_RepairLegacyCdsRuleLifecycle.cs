using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMRS.Api.Migrations
{
    /// <inheritdoc />
    public partial class RepairLegacyCdsRuleLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data repair for rules created before AddCdsRuleLifecycle (20260517094748).
            // That migration added Status (NOT NULL DEFAULT '') plus the publish/version model
            // but never backfilled existing rows. As a result legacy rules kept an invalid
            // empty Status and had no CdsRuleVersion snapshot, so GetActiveByHookAsync
            // (which requires Status = 'Published' joined to a PublishedVersion) silently
            // stopped returning them and previously-live rules no longer fired.

            // 1. Recreate a v1 published snapshot for legacy rules that were live (IsActive) but
            //    were never migrated into the version model. Mirrors RuleManagementService.PublishAsync.
            migrationBuilder.Sql(
                """
                INSERT INTO "CdsRuleVersions"
                    ("Id", "RuleDefinitionId", "VersionNumber", "HookId", "Name", "Description",
                     "Priority", "ExpressionJson", "CardTemplateJson", "IsActive", "PublishedAt", "PublishedBy")
                SELECT gen_random_uuid(), d."Id", 1, d."HookId", d."Name", d."Description",
                       d."Priority", d."ExpressionJson", d."CardTemplateJson", TRUE, now(), 'system-migration'
                FROM "CdsRuleDefinitions" AS d
                WHERE (d."Status" IS NULL OR d."Status" = '')
                  AND d."IsActive" = TRUE
                  AND d."PublishedVersionId" IS NULL;
                """);

            // 2. Point those definitions at their new published version and mark them Published.
            migrationBuilder.Sql(
                """
                UPDATE "CdsRuleDefinitions" AS d
                SET "Status" = 'Published',
                    "HasUnpublishedChanges" = FALSE,
                    "PublishedVersionId" = v."Id",
                    "PublishedVersionNumber" = 1,
                    "PublishedAt" = v."PublishedAt",
                    "PublishedBy" = 'system-migration'
                FROM "CdsRuleVersions" AS v
                WHERE v."RuleDefinitionId" = d."Id"
                  AND v."VersionNumber" = 1
                  AND (d."Status" IS NULL OR d."Status" = '')
                  AND d."IsActive" = TRUE
                  AND d."PublishedVersionId" IS NULL;
                """);

            // 3. Any remaining legacy rows (never active) become valid drafts instead of empty status.
            migrationBuilder.Sql(
                """
                UPDATE "CdsRuleDefinitions"
                SET "Status" = 'Draft',
                    "HasUnpublishedChanges" = TRUE
                WHERE "Status" IS NULL OR "Status" = '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best-effort revert: drop the snapshots this migration created and return the
            // affected definitions to the pre-repair (empty status, unpublished) state.
            migrationBuilder.Sql(
                """
                UPDATE "CdsRuleDefinitions" AS d
                SET "Status" = '',
                    "PublishedVersionId" = NULL,
                    "PublishedVersionNumber" = NULL,
                    "PublishedAt" = NULL,
                    "PublishedBy" = NULL
                FROM "CdsRuleVersions" AS v
                WHERE v."RuleDefinitionId" = d."Id"
                  AND v."PublishedBy" = 'system-migration'
                  AND d."PublishedVersionId" = v."Id";
                """);

            migrationBuilder.Sql(
                """
                DELETE FROM "CdsRuleVersions" WHERE "PublishedBy" = 'system-migration';
                """);
        }
    }
}
