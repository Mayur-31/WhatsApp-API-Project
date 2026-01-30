using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverConnectApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRetryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NextRetryAt",
                table: "Messages",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetryCount",
                table: "Messages",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TeamId",
                table: "Messages",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Messages_TeamId",
                table: "Messages",
                column: "TeamId");

            migrationBuilder.AddForeignKey(
                name: "FK_Messages_Teams_TeamId",
                table: "Messages",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Messages_Teams_TeamId",
                table: "Messages");

            migrationBuilder.DropIndex(
                name: "IX_Messages_TeamId",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "NextRetryAt",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "RetryCount",
                table: "Messages");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "Messages");
        }
    }
}
