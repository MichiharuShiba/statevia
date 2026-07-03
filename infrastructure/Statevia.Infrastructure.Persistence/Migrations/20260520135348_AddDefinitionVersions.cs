using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Statevia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public partial class AddDefinitionVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.CreateTable(
                name: "definitions",
                columns: table => new
                {
                    definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    latest_version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_definitions", x => x.definition_id);
                });

            migrationBuilder.CreateTable(
                name: "definition_versions",
                columns: table => new
                {
                    definition_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    source_yaml = table.Column<string>(type: "text", nullable: false),
                    compiled_json = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_definition_versions", x => x.definition_version_id);
                    table.ForeignKey(
                        name: "FK_definition_versions_definitions_definition_id",
                        column: x => x.definition_id,
                        principalTable: "definitions",
                        principalColumn: "definition_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_definition_versions_definition_id_version",
                table: "definition_versions",
                columns: new[] { "definition_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_definitions_tenant_id_slug",
                table: "definitions",
                columns: new[] { "tenant_id", "slug" },
                unique: true);

            // workflow_definitions → definitions / definition_versions (version=1)
            migrationBuilder.Sql(
                """
                INSERT INTO definitions (definition_id, tenant_id, project_id, slug, name, latest_version, created_at, updated_at)
                SELECT
                    wd.definition_id,
                    wd.tenant_id,
                    NULL,
                    LEFT(
                        COALESCE(NULLIF(TRIM(wd.name), ''), 'definition') || '-' || SUBSTRING(wd.definition_id::text, 1, 8),
                        128),
                    wd.name,
                    1,
                    wd.created_at,
                    wd.updated_at
                FROM workflow_definitions wd;
                """);

            migrationBuilder.Sql(
                """
                INSERT INTO definition_versions (definition_version_id, definition_id, version, source_yaml, compiled_json, created_at)
                SELECT
                    gen_random_uuid(),
                    wd.definition_id,
                    1,
                    wd.source_yaml,
                    wd.compiled_json,
                    wd.created_at
                FROM workflow_definitions wd;
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "definition_version_id",
                table: "workflows",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE workflows w
                SET definition_version_id = dv.definition_version_id
                FROM definition_versions dv
                WHERE dv.definition_id = w.definition_id
                  AND dv.version = 1;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "definition_version_id",
                table: "workflows",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_workflows_definition_version_id",
                table: "workflows",
                column: "definition_version_id");

            migrationBuilder.AddForeignKey(
                name: "FK_workflows_definition_versions_definition_version_id",
                table: "workflows",
                column: "definition_version_id",
                principalTable: "definition_versions",
                principalColumn: "definition_version_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropForeignKey(
                name: "FK_workflows_definition_versions_definition_version_id",
                table: "workflows");

            migrationBuilder.DropTable(
                name: "definition_versions");

            migrationBuilder.DropTable(
                name: "definitions");

            migrationBuilder.DropIndex(
                name: "IX_workflows_definition_version_id",
                table: "workflows");

            migrationBuilder.DropColumn(
                name: "definition_version_id",
                table: "workflows");
        }
    }
}
