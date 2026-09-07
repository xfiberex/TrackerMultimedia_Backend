using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrackerMultimedia.Migrations
{
    /// <summary>
    /// Retira las cinco columnas de procedencia externa de <c>MediaItems</c> y el índice
    /// único parcial que las acompañaba. Van con la pantalla «Descubrir», eliminada el
    /// 2026-09-06: sin importación desde catálogos no hay nada que pueda escribirlas.
    ///
    /// <para><b>Es destructiva y no se puede deshacer del todo.</b> El <c>Down</c>
    /// devuelve las columnas y el índice, pero vacíos: los valores no se guardan en
    /// ninguna parte. Se aceptó porque al aplicarla la tabla tenía <b>0 filas</b>,
    /// comprobado el 2026-09-06 antes de escribirla. Sobre una base con datos importados
    /// de un catálogo habría que exportar antes.</para>
    ///
    /// <para><c>CoverImageUrl</c> y <c>ReferenceUrl</c> siguen donde estaban: no eran de
    /// los catálogos, son dos direcciones que el usuario escribe él mismo.</para>
    /// </summary>
    public partial class RemoveExternalCatalogFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MediaItems_UserId_SourceType_ExternalId_ExternalMediaKind",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "ExternalMediaKind",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "ExternalScore",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "ExternalStatusLabel",
                table: "MediaItems");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "MediaItems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ExternalId",
                table: "MediaItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExternalMediaKind",
                table: "MediaItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "ExternalScore",
                table: "MediaItems",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalStatusLabel",
                table: "MediaItems",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceType",
                table: "MediaItems",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_UserId_SourceType_ExternalId_ExternalMediaKind",
                table: "MediaItems",
                columns: new[] { "UserId", "SourceType", "ExternalId", "ExternalMediaKind" },
                unique: true,
                filter: "\"ExternalId\" IS NOT NULL AND \"ExternalMediaKind\" IS NOT NULL");
        }
    }
}
