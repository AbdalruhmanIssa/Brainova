using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brainova.DAL.Migrations
{
    /// <inheritdoc />
    public partial class ai : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PredictionResult = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProbabilitiesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    GradcamFileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PredictedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiResults_MriCases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "MriCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiResults_CaseId",
                table: "AiResults",
                column: "CaseId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiResults");
        }
    }
}
