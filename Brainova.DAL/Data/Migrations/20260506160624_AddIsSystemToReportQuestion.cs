using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brainova.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddIsSystemToReportQuestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSystem",
                table: "ReportQuestions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(@"
        UPDATE ReportQuestions
        SET IsSystem = 1
        WHERE LOWER(LTRIM(RTRIM(Code))) = 'preliminary assesment';
    ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSystem",
                table: "ReportQuestions");
        }
    }
}
