using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brainova.DAL.Migrations
{
    /// <inheritdoc />
    public partial class skipNoTumor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SkipWhenNoTumor",
                table: "ReportQuestions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(@"
        UPDATE ReportQuestions
        SET SkipWhenNoTumor = 1
        WHERE LOWER(LTRIM(RTRIM(Code))) IN ('tumor size', 'tumor location', 'functional impact');
    ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "SkipWhenNoTumor", table: "ReportQuestions");
        }
    }
}
