using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MobileAlert.Api.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceAlertTargetWithArrayAndAuditPayloads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertTargets");

            migrationBuilder.AddColumn<string>(
                name: "RequestPayload",
                table: "Alerts",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponsePayload",
                table: "Alerts",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int[]>(
                name: "TargetFirefighterIds",
                table: "Alerts",
                type: "integer[]",
                nullable: false,
                defaultValue: new int[0]);

            migrationBuilder.AddColumn<string>(
                name: "WebhookRequestPayload",
                table: "AlertResponses",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebhookResponsePayload",
                table: "AlertResponses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WebhookStatusCode",
                table: "AlertResponses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebhookUrl",
                table: "AlertResponses",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequestPayload",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "ResponsePayload",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "TargetFirefighterIds",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "WebhookRequestPayload",
                table: "AlertResponses");

            migrationBuilder.DropColumn(
                name: "WebhookResponsePayload",
                table: "AlertResponses");

            migrationBuilder.DropColumn(
                name: "WebhookStatusCode",
                table: "AlertResponses");

            migrationBuilder.DropColumn(
                name: "WebhookUrl",
                table: "AlertResponses");

            migrationBuilder.CreateTable(
                name: "AlertTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AlertId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirefighterId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlertTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlertTargets_Alerts_AlertId",
                        column: x => x.AlertId,
                        principalTable: "Alerts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AlertTargets_Firefighters_FirefighterId",
                        column: x => x.FirefighterId,
                        principalTable: "Firefighters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlertTargets_AlertId_FirefighterId",
                table: "AlertTargets",
                columns: new[] { "AlertId", "FirefighterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AlertTargets_FirefighterId",
                table: "AlertTargets",
                column: "FirefighterId");
        }
    }
}
