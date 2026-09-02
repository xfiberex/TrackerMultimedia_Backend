using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackerMultimedia.Migrations
{
    /// <inheritdoc />
    public partial class AddUserFormatIdFixLoginsFkAndIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserLogins_AspNetUsers_ApplicationUserId",
                table: "AspNetUserLogins");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUserLogins_ApplicationUserId",
                table: "AspNetUserLogins");

            migrationBuilder.DropColumn(
                name: "ApplicationUserId",
                table: "AspNetUserLogins");

            migrationBuilder.AddColumn<Guid>(
                name: "UserFormatId",
                table: "MediaItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_UserFormatId",
                table: "MediaItems",
                column: "UserFormatId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_UserId_CreatedAtUtc",
                table: "MediaItems",
                columns: new[] { "UserId", "CreatedAtUtc" });

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
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_MediaItems_UserFormatId",
                table: "MediaItems");

            migrationBuilder.DropIndex(
                name: "IX_MediaItems_UserId_CreatedAtUtc",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "UserFormatId",
                table: "MediaItems");

            migrationBuilder.AddColumn<Guid>(
                name: "ApplicationUserId",
                table: "AspNetUserLogins",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_ApplicationUserId",
                table: "AspNetUserLogins",
                column: "ApplicationUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserLogins_AspNetUsers_ApplicationUserId",
                table: "AspNetUserLogins",
                column: "ApplicationUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }
    }
}
