using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brainova.DAL.Migrations
{
    /// <inheritdoc />
    public partial class addingsupervisortoreportquestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReportQuestions_Code",
                table: "ReportQuestions");

            migrationBuilder.AddColumn<string>(
                name: "SupervisorId",
                table: "ReportQuestions",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.Sql(@"
        UPDATE ReportQuestions
        SET SupervisorId = 'af17af76-ccae-44ca-b51d-004520431992'
        WHERE SupervisorId IS NULL
    ");

            migrationBuilder.AlterColumn<string>(
                name: "SupervisorId",
                table: "ReportQuestions",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportQuestions_SupervisorId",
                table: "ReportQuestions",
                column: "SupervisorId");

            migrationBuilder.CreateIndex(
                name: "IX_ReportQuestions_SupervisorId_Code",
                table: "ReportQuestions",
                columns: new[] { "SupervisorId", "Code" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ReportQuestions_AspNetUsers_SupervisorId",
                table: "ReportQuestions",
                column: "SupervisorId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReportQuestions_AspNetUsers_SupervisorId",
                table: "ReportQuestions");

            migrationBuilder.DropIndex(
                name: "IX_ReportQuestions_SupervisorId",
                table: "ReportQuestions");

            migrationBuilder.DropIndex(
                name: "IX_ReportQuestions_SupervisorId_Code",
                table: "ReportQuestions");

            migrationBuilder.DropColumn(
                name: "SupervisorId",
                table: "ReportQuestions");

            migrationBuilder.CreateIndex(
                name: "IX_ReportQuestions_Code",
                table: "ReportQuestions",
                column: "Code",
                unique: true);
        }
    }
}
