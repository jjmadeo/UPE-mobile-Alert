using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MobileAlert.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCorrelationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CorrelationId",
                table: "Alerts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_CorrelationId",
                table: "Alerts",
                column: "CorrelationId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Alerts_CorrelationId",
                table: "Alerts");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "Alerts");
        }
    }
}
