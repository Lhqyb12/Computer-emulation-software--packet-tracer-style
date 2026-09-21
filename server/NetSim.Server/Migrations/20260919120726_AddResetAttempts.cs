using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetSim.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddResetAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ResetAttempts",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResetAttempts",
                table: "Users");
        }
    }
}
