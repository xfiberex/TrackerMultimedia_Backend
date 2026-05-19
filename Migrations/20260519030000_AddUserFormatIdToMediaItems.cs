using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackerMultimedia.Migrations
{
    /// <inheritdoc />
    public partial class AddUserFormatIdToMediaItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserFormatId",
                table: "MediaItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_UserFormatId",
                table: "MediaItems",
                column: "UserFormatId");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaItems_UserFormats_UserFormatId",
                table: "MediaItems",
                column: "UserFormatId",
                principalTable: "UserFormats",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MediaItems_UserFormats_UserFormatId",
                table: "MediaItems");

            migrationBuilder.DropIndex(
                name: "IX_MediaItems_UserFormatId",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "UserFormatId",
                table: "MediaItems");
        }
    }
}
