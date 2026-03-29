using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SellBotLk.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class _20260322_AddDriverNoteToOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DriverNote",
                table: "Orders",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DriverNote",
                table: "Orders");
        }
    }
}
