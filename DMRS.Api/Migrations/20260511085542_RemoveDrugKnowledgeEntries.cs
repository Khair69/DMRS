using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMRS.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDrugKnowledgeEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DrugKnowledgeEntries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DrugKnowledgeEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    KnowledgeType = table.Column<string>(type: "text", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    QueryKey = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrugKnowledgeEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DrugKnowledgeEntries_QueryKey_KnowledgeType_Source",
                table: "DrugKnowledgeEntries",
                columns: new[] { "QueryKey", "KnowledgeType", "Source" });
        }
    }
}
