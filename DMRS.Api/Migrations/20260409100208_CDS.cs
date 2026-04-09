using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMRS.Api.Migrations
{
    /// <inheritdoc />
    public partial class CDS : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CdsRuleDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HookId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ExpressionJson = table.Column<string>(type: "jsonb", nullable: false),
                    CardTemplateJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CdsRuleDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DrugKnowledgeEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QueryKey = table.Column<string>(type: "text", nullable: false),
                    KnowledgeType = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    FetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrugKnowledgeEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CdsRuleDefinitions_HookId_IsActive",
                table: "CdsRuleDefinitions",
                columns: new[] { "HookId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_DrugKnowledgeEntries_QueryKey_KnowledgeType_Source",
                table: "DrugKnowledgeEntries",
                columns: new[] { "QueryKey", "KnowledgeType", "Source" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CdsRuleDefinitions");

            migrationBuilder.DropTable(
                name: "DrugKnowledgeEntries");
        }
    }
}
