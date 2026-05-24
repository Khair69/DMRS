using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMRS.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMedicineKnowledgeRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MedicineKnowledgeRecords",
                columns: table => new
                {
                    RxCui = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    MaxDailyMg = table.Column<decimal>(type: "numeric", nullable: true),
                    MaxSingleMg = table.Column<decimal>(type: "numeric", nullable: true),
                    WarningThresholdMg = table.Column<decimal>(type: "numeric", nullable: true),
                    PregnancyCategory = table.Column<string>(type: "text", nullable: true),
                    IsControlled = table.Column<bool>(type: "boolean", nullable: true),
                    IngredientCodesJson = table.Column<string>(type: "jsonb", nullable: false),
                    IngredientNamesJson = table.Column<string>(type: "jsonb", nullable: false),
                    IndicationCodesJson = table.Column<string>(type: "jsonb", nullable: false),
                    IngredientSearchText = table.Column<string>(type: "text", nullable: false),
                    IndicationSearchText = table.Column<string>(type: "text", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    FetchedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicineKnowledgeRecords", x => x.RxCui);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MedicineKnowledgeRecords_Name",
                table: "MedicineKnowledgeRecords",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MedicineKnowledgeRecords");
        }
    }
}
