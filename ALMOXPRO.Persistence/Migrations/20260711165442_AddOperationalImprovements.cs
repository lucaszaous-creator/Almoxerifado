using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ALMOXPRO.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalImprovements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "AccessEndTime",
                table: "users",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<TimeOnly>(
                name: "AccessStartTime",
                table: "users",
                type: "time without time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WeekdaysOnly",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "stock_items",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<int>(
                name: "ReversedByEntryId",
                table: "material_exits",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccessEndTime",
                table: "users");

            migrationBuilder.DropColumn(
                name: "AccessStartTime",
                table: "users");

            migrationBuilder.DropColumn(
                name: "WeekdaysOnly",
                table: "users");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "stock_items");

            migrationBuilder.DropColumn(
                name: "ReversedByEntryId",
                table: "material_exits");
        }
    }
}
