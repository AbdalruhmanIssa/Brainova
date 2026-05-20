using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brainova.DAL.Migrations
{
    public partial class BackfillReportCode : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE Reports
                SET ReportCode =
                    'REP-' + CAST(YEAR(SubmittedAt) AS varchar(4)) + '-' +
                    RIGHT('000000' + CAST(ReportNumber AS varchar(20)), 6)
                WHERE ReportNumber IS NOT NULL
                  AND ReportCode IS NULL
            """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE Reports
                SET ReportCode = NULL
                WHERE ReportCode LIKE 'REP-%'
            """);
        }
    }
}