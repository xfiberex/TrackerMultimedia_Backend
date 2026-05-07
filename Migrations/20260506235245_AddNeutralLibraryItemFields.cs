using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackerMultimedia.Migrations
{
    /// <inheritdoc />
    public partial class AddNeutralLibraryItemFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Type",
                table: "MediaItems",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAtUtc",
                table: "MediaItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContentKind",
                table: "MediaItems",
                type: "integer",
                nullable: false,
                defaultValue: 9);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "MediaItems",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProgressCurrent",
                table: "MediaItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ProgressTotal",
                table: "MediaItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProgressUnit",
                table: "MediaItems",
                type: "integer",
                nullable: false,
                defaultValue: 8);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAtUtc",
                table: "MediaItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "MediaItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                @"UPDATE ""MediaItems""
SET ""ContentKind"" = CASE
        WHEN ""Type"" IN (1, 3) THEN 1
        WHEN ""Type"" IN (2, 4, 5) THEN 4
        ELSE 9
    END,
    ""ProgressCurrent"" = ""ProgressCount"",
    ""ProgressUnit"" = CASE
        WHEN ""Type"" IN (1, 3) THEN 1
        WHEN ""Type"" IN (2, 4, 5) THEN 2
        ELSE 8
    END,
    ""UpdatedAtUtc"" = ""CreatedAtUtc"";");

            migrationBuilder.AlterColumn<DateTime>(
                name: "UpdatedAtUtc",
                table: "MediaItems",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedAtUtc",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "ContentKind",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "ProgressCurrent",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "ProgressTotal",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "ProgressUnit",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "StartedAtUtc",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "UpdatedAtUtc",
                table: "MediaItems");

            migrationBuilder.AlterColumn<int>(
                name: "Type",
                table: "MediaItems",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
