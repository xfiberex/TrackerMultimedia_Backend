using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackerMultimedia.Migrations
{
    /// <inheritdoc />
    public partial class ExtendExternalIdUniqueIndexToSourceType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MediaItems_UserId_ExternalId_ExternalMediaKind",
                table: "MediaItems");

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_UserId_SourceType_ExternalId_ExternalMediaKind",
                table: "MediaItems",
                columns: new[] { "UserId", "SourceType", "ExternalId", "ExternalMediaKind" },
                unique: true,
                filter: "\"ExternalId\" IS NOT NULL AND \"ExternalMediaKind\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MediaItems_UserId_SourceType_ExternalId_ExternalMediaKind",
                table: "MediaItems");

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_UserId_ExternalId_ExternalMediaKind",
                table: "MediaItems",
                columns: new[] { "UserId", "ExternalId", "ExternalMediaKind" },
                unique: true,
                filter: "\"ExternalId\" IS NOT NULL AND \"ExternalMediaKind\" IS NOT NULL");
        }
    }
}
