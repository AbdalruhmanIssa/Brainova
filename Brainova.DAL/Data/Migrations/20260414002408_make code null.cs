using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brainova.DAL.Migrations
{
    public partial class makecodenull : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reports_ReportCode",
                table: "Reports");

            migrationBuilder.AlterColumn<string>(
                name: "ReportCode",
                table: "Reports",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReportCode",
                table: "Reports",
                column: "ReportCode",
                unique: true,
                filter: "[ReportCode] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reports_ReportCode",
                table: "Reports");

            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM Reports WHERE ReportCode IS NULL)
                    THROW 50000, 'Cannot make ReportCode NOT NULL because NULL values exist.', 1;
            """);

            migrationBuilder.AlterColumn<string>(
                name: "ReportCode",
                table: "Reports",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReportCode",
                table: "Reports",
                column: "ReportCode",
                unique: true,
                filter: "[ReportCode] IS NOT NULL");
        }
    }
}