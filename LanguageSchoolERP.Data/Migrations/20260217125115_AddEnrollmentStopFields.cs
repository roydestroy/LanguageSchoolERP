using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageSchoolERP.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEnrollmentStopFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsStopped",
                table: "Enrollments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "StopReason",
                table: "Enrollments",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "StoppedAmountWaived",
                table: "Enrollments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "StoppedOn",
                table: "Enrollments",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsStopped",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "StopReason",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "StoppedAmountWaived",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "StoppedOn",
                table: "Enrollments");
        }
    }
}
