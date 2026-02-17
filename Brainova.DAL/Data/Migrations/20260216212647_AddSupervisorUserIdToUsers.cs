using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brainova.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddSupervisorUserIdToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SupervisorUserId",
                table: "AspNetUsers",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_SupervisorUserId",
                table: "AspNetUsers",
                column: "SupervisorUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_AspNetUsers_SupervisorUserId",
                table: "AspNetUsers",
                column: "SupervisorUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_AspNetUsers_SupervisorUserId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_SupervisorUserId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "SupervisorUserId",
                table: "AspNetUsers");
        }
    }
}
