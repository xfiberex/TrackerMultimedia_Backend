using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackerMultimedia.Migrations
{
    /// <inheritdoc />
    public partial class RemoveContentKindFromUserFormats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentKind",
                table: "UserFormats");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContentKind",
                table: "UserFormats",
                type: "integer",
                nullable: true);
        }
    }
}
