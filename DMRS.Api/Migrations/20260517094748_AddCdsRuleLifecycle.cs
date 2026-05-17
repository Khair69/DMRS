using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMRS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCdsRuleLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CdsRuleDefinitions_HookId_IsActive",
                table: "CdsRuleDefinitions");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "CdsRuleDefinitions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasUnpublishedChanges",
                table: "CdsRuleDefinitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PublishedAt",
                table: "CdsRuleDefinitions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublishedBy",
                table: "CdsRuleDefinitions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PublishedVersionId",
                table: "CdsRuleDefinitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PublishedVersionNumber",
                table: "CdsRuleDefinitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "CdsRuleDefinitions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "CdsRuleDefinitions",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CdsRuleVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    HookId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    ExpressionJson = table.Column<string>(type: "jsonb", nullable: false),
                    CardTemplateJson = table.Column<string>(type: "jsonb", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CdsRuleVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CdsRuleVersions_CdsRuleDefinitions_RuleDefinitionId",
                        column: x => x.RuleDefinitionId,
                        principalTable: "CdsRuleDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CdsRuleDefinitions_HookId_Status_IsActive",
                table: "CdsRuleDefinitions",
                columns: new[] { "HookId", "Status", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CdsRuleDefinitions_PublishedVersionId",
                table: "CdsRuleDefinitions",
                column: "PublishedVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_CdsRuleVersions_HookId",
                table: "CdsRuleVersions",
                column: "HookId");

            migrationBuilder.CreateIndex(
                name: "IX_CdsRuleVersions_RuleDefinitionId_VersionNumber",
                table: "CdsRuleVersions",
                columns: new[] { "RuleDefinitionId", "VersionNumber" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CdsRuleDefinitions_CdsRuleVersions_PublishedVersionId",
                table: "CdsRuleDefinitions",
                column: "PublishedVersionId",
                principalTable: "CdsRuleVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CdsRuleDefinitions_CdsRuleVersions_PublishedVersionId",
                table: "CdsRuleDefinitions");

            migrationBuilder.DropTable(
                name: "CdsRuleVersions");

            migrationBuilder.DropIndex(
                name: "IX_CdsRuleDefinitions_HookId_Status_IsActive",
                table: "CdsRuleDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_CdsRuleDefinitions_PublishedVersionId",
                table: "CdsRuleDefinitions");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "CdsRuleDefinitions");

            migrationBuilder.DropColumn(
                name: "HasUnpublishedChanges",
                table: "CdsRuleDefinitions");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "CdsRuleDefinitions");

            migrationBuilder.DropColumn(
                name: "PublishedBy",
                table: "CdsRuleDefinitions");

            migrationBuilder.DropColumn(
                name: "PublishedVersionId",
                table: "CdsRuleDefinitions");

            migrationBuilder.DropColumn(
                name: "PublishedVersionNumber",
                table: "CdsRuleDefinitions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "CdsRuleDefinitions");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "CdsRuleDefinitions");

            migrationBuilder.CreateIndex(
                name: "IX_CdsRuleDefinitions_HookId_IsActive",
                table: "CdsRuleDefinitions",
                columns: new[] { "HookId", "IsActive" });
        }
    }
}
