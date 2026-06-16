using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Cetee.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "Tasks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledStart",
                table: "Tasks",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "ScheduledStart",
                table: "Tasks");
        }
    }
}
