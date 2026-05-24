using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DMRS.Api.Migrations
{
    /// <inheritdoc />
    public partial class BackToBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DrugMappings");

            migrationBuilder.DropTable(
                name: "DrugMaxDoses");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DrugMappings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IngredientRxCui = table.Column<string>(type: "text", nullable: false),
                    LastUpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SourceSystem = table.Column<string>(type: "text", nullable: false),
                    SourceTerm = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrugMappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DrugMaxDoses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Display = table.Column<string>(type: "text", nullable: true),
                    IngredientRxCui = table.Column<string>(type: "text", nullable: false),
                    LastUpdatedUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MaxDailyDoseMg = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrugMaxDoses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DrugMappings_SourceTerm_SourceSystem_IngredientRxCui",
                table: "DrugMappings",
                columns: new[] { "SourceTerm", "SourceSystem", "IngredientRxCui" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DrugMaxDoses_IngredientRxCui",
                table: "DrugMaxDoses",
                column: "IngredientRxCui",
                unique: true);
        }
    }
}
