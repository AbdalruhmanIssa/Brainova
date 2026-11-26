using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Brainova.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordResetAttemptLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ResetPasswordFailedAttempts",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResetPasswordLockedUntil",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            // ---------------------------------------------------------------------------
            // Feedbacks: the model has had this table since March, but the migration that
            // created it (20260324204657_AddFeedbackTable) was deleted by the
            // "Revert taima changes" commit (2658b88), so the model snapshot never learned
            // about it and this migration tried to create it again.
            // Existing databases (local, Azure) already have the table; a brand-new database
            // does not. So create the table and each index only if it is missing.
            // Each Sql() call is a separate batch, so the indexes compile after the table exists.
            // ---------------------------------------------------------------------------
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[dbo].[Feedbacks]', N'U') IS NULL
BEGIN
    CREATE TABLE [Feedbacks] (
        [Id] uniqueidentifier NOT NULL,
        [ReportId] uniqueidentifier NOT NULL,
        [SupervisorId] nvarchar(450) NOT NULL,
        [StudentId] nvarchar(450) NOT NULL,
        [Comment] nvarchar(3000) NOT NULL,
        [IsSeen] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_Feedbacks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Feedbacks_AspNetUsers_StudentId] FOREIGN KEY ([StudentId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Feedbacks_AspNetUsers_SupervisorId] FOREIGN KEY ([SupervisorId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Feedbacks_Reports_ReportId] FOREIGN KEY ([ReportId]) REFERENCES [Reports] ([Id]) ON DELETE CASCADE
    );
END");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Feedbacks_ReportId' AND object_id = OBJECT_ID(N'[dbo].[Feedbacks]'))
    CREATE UNIQUE INDEX [IX_Feedbacks_ReportId] ON [Feedbacks] ([ReportId]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Feedbacks_StudentId' AND object_id = OBJECT_ID(N'[dbo].[Feedbacks]'))
    CREATE INDEX [IX_Feedbacks_StudentId] ON [Feedbacks] ([StudentId]);");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Feedbacks_SupervisorId' AND object_id = OBJECT_ID(N'[dbo].[Feedbacks]'))
    CREATE INDEX [IX_Feedbacks_SupervisorId] ON [Feedbacks] ([SupervisorId]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Feedbacks is deliberately NOT dropped: on existing databases it holds real data
            // that predates this migration.

            migrationBuilder.DropColumn(
                name: "ResetPasswordFailedAttempts",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ResetPasswordLockedUntil",
                table: "AspNetUsers");
        }
    }
}
