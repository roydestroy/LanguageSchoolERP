using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LanguageSchoolERP.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReceiptCounterPerAcademicYear : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Add the column as NULLABLE first
            migrationBuilder.AddColumn<Guid>(
                name: "AcademicPeriodId",
                table: "ReceiptCounters",
                type: "uniqueidentifier",
                nullable: true);

            // 2) Backfill existing rows to a valid AcademicPeriodId (pick current year)
            migrationBuilder.Sql(@"
            UPDATE rc
            SET AcademicPeriodId = ap.AcademicPeriodId
            FROM ReceiptCounters rc
            CROSS APPLY (
                SELECT TOP 1 AcademicPeriodId
                FROM AcademicPeriods
                ORDER BY IsCurrent DESC, Name DESC
            ) ap
            WHERE rc.AcademicPeriodId IS NULL
               OR rc.AcademicPeriodId = '00000000-0000-0000-0000-000000000000';
            ");

            // 3) Make the column NOT NULL
            migrationBuilder.AlterColumn<Guid>(
                name: "AcademicPeriodId",
                table: "ReceiptCounters",
                type: "uniqueidentifier",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            // 4) Add index + FK
            migrationBuilder.CreateIndex(
                name: "IX_ReceiptCounters_AcademicPeriodId",
                table: "ReceiptCounters",
                column: "AcademicPeriodId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ReceiptCounters_AcademicPeriods_AcademicPeriodId",
                table: "ReceiptCounters",
                column: "AcademicPeriodId",
                principalTable: "AcademicPeriods",
                principalColumn: "AcademicPeriodId",
                onDelete: ReferentialAction.Cascade);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ReceiptCounters_AcademicPeriods_AcademicPeriodId",
                table: "ReceiptCounters");

            migrationBuilder.DropIndex(
                name: "IX_ReceiptCounters_AcademicPeriodId",
                table: "ReceiptCounters");

            migrationBuilder.DropColumn(
                name: "AcademicPeriodId",
                table: "ReceiptCounters");
        }
    }
}
