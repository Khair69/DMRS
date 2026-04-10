using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DMRS.MedicineInfo.Api.Migrations
{
    /// <inheritdoc />
    public partial class ALL : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Ingredients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ingredients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Medicines",
                columns: table => new
                {
                    RxCui = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Dosing_MaxDailyMg = table.Column<decimal>(type: "numeric", nullable: false),
                    Dosing_MaxSingleMg = table.Column<decimal>(type: "numeric", nullable: false),
                    Dosing_WarningThreshold = table.Column<decimal>(type: "numeric", nullable: false),
                    Safety_PregnancyCategory = table.Column<string>(type: "text", nullable: false),
                    Safety_IsControlled = table.Column<bool>(type: "boolean", nullable: false),
                    Indications = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Medicines", x => x.RxCui);
                });

            migrationBuilder.CreateTable(
                name: "MedicineIngredients",
                columns: table => new
                {
                    IngredientsId = table.Column<int>(type: "integer", nullable: false),
                    MedicinesRxCui = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicineIngredients", x => new { x.IngredientsId, x.MedicinesRxCui });
                    table.ForeignKey(
                        name: "FK_MedicineIngredients_Ingredients_IngredientsId",
                        column: x => x.IngredientsId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MedicineIngredients_Medicines_MedicinesRxCui",
                        column: x => x.MedicinesRxCui,
                        principalTable: "Medicines",
                        principalColumn: "RxCui",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MedicineIngredients_MedicinesRxCui",
                table: "MedicineIngredients",
                column: "MedicinesRxCui");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MedicineIngredients");

            migrationBuilder.DropTable(
                name: "Ingredients");

            migrationBuilder.DropTable(
                name: "Medicines");
        }
    }
}
