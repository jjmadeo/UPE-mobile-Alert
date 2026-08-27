using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MobileAlert.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertTargets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlertTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AlertId = table.Column<Guid>(type: "uuid", nullable: false),
                    FirefighterId = table.Column<int>(type: "integer", nullable: false)
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlertTargets");
        }
    }
}
