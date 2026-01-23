using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DriverConnectApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLocalMessageIdToMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LocalMessageId",
                table: "Messages",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LocalMessageId",
                table: "Messages");
        }
    }
}
