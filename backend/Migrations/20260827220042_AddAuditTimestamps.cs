using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileAlert.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditTimestamps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Webhooks",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Institutions",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Institutions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Firefighters",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Firefighters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "DeviceTokens",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "DeviceTokens",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ApiKeys",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "AlertTargets",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "AlertTargets",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Alerts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "AlertResponses",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "AlertResponses",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Webhooks");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Institutions");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Firefighters");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Firefighters");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "DeviceTokens");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "DeviceTokens");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "AlertTargets");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "AlertTargets");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "AlertResponses");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "AlertResponses");
        }
    }
}
