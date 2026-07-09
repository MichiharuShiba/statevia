using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Statevia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDefinitionsDeletedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_definitions_project_id_slug",
                table: "definitions");

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "definitions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_definitions_project_id_slug",
                table: "definitions",
                columns: new[] { "project_id", "slug" },
                unique: true,
                filter: "\"deleted_at\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_definitions_project_id_slug",
                table: "definitions");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "definitions");

            migrationBuilder.CreateIndex(
                name: "IX_definitions_project_id_slug",
                table: "definitions",
                columns: new[] { "project_id", "slug" },
                unique: true);
        }
    }
}
