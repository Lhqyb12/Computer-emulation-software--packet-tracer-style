using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NetSim.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmailVerified",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "VerifyAttempts",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifyCodeExpiresAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerifyCodeHash",
                table: "Users",
                type: "text",
                nullable: true);
                
            // Everyone who registered before this feature existed is treated as already verified -
            // otherwise every existing account (the admin included) would be locked out of the app
            migrationBuilder.Sql("UPDATE \"Users\" SET \"EmailVerified\" = TRUE;");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmailVerified",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "VerifyAttempts",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "VerifyCodeExpiresAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "VerifyCodeHash",
                table: "Users");
        }
    }
}
