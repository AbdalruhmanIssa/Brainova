using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brainova.DAL.Migrations
{
    /// <inheritdoc />
    public partial class chanigingreportanswers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnswerBool",
                table: "ReportAnswers");

            migrationBuilder.DropColumn(
                name: "AnswerJson",
                table: "ReportAnswers");

            migrationBuilder.DropColumn(
                name: "AnswerNumber",
                table: "ReportAnswers");

            migrationBuilder.DropColumn(
                name: "AnswerText",
                table: "ReportAnswers");

            migrationBuilder.AddColumn<string>(
                name: "AnswerValue",
                table: "ReportAnswers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnswerValue",
                table: "ReportAnswers");

            migrationBuilder.AddColumn<bool>(
                name: "AnswerBool",
                table: "ReportAnswers",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnswerJson",
                table: "ReportAnswers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AnswerNumber",
                table: "ReportAnswers",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AnswerText",
                table: "ReportAnswers",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
