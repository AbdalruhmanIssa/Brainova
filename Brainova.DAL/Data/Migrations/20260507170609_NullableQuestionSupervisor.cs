using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brainova.DAL.Migrations
{
    /// <inheritdoc />
    public partial class NullableQuestionSupervisor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReportQuestions_SupervisorId_Code",
                table: "ReportQuestions");

            migrationBuilder.DropIndex(
                name: "IX_ReportQuestions_SupervisorId_Order",
                table: "ReportQuestions");

            migrationBuilder.AlterColumn<string>(
                name: "SupervisorId",
                table: "ReportQuestions",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450);

            migrationBuilder.CreateIndex(
                name: "IX_ReportQuestions_SupervisorId_Code",
                table: "ReportQuestions",
                columns: new[] { "SupervisorId", "Code" },
                unique: true,
                filter: "[SupervisorId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReportQuestions_SupervisorId_Order",
                table: "ReportQuestions",
                columns: new[] { "SupervisorId", "Order" },
                unique: true,
                filter: "[SupervisorId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ReportQuestions_SupervisorId_Code",
                table: "ReportQuestions");

            migrationBuilder.DropIndex(
                name: "IX_ReportQuestions_SupervisorId_Order",
                table: "ReportQuestions");

            migrationBuilder.AlterColumn<string>(
                name: "SupervisorId",
                table: "ReportQuestions",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportQuestions_SupervisorId_Code",
                table: "ReportQuestions",
                columns: new[] { "SupervisorId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportQuestions_SupervisorId_Order",
                table: "ReportQuestions",
                columns: new[] { "SupervisorId", "Order" },
                unique: true);
        }
    }
}
