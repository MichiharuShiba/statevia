using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Statevia.Service.Api.Migrations
{
    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public partial class AddProjectsAndProjectAccesses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    display_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    visibility = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_public = table.Column<bool>(type: "boolean", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.project_id);
                    table.ForeignKey(
                        name: "FK_projects_tenants_owner_tenant_id",
                        column: x => x.owner_tenant_id,
                        principalTable: "tenants",
                        principalColumn: "tenant_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_accesses",
                columns: table => new
                {
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_project_accesses", x => new { x.project_id, x.tenant_id });
                    table.ForeignKey(
                        name: "FK_project_accesses_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "project_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_project_accesses_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "tenant_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_project_accesses_tenant_id",
                table: "project_accesses",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "IX_projects_owner_tenant_id_slug",
                table: "projects",
                columns: new[] { "owner_tenant_id", "slug" },
                unique: true);

            // テナントごとに slug=default の project を生成し、definitions.project_id を埋める
            migrationBuilder.Sql(
                """
                INSERT INTO projects (project_id, owner_tenant_id, slug, display_name, visibility, is_public, description, created_at)
                SELECT
                    gen_random_uuid(),
                    t.tenant_id,
                    'default',
                    t.display_name || ' default',
                    'Private',
                    false,
                    'Migration default project',
                    NOW()
                FROM (
                    SELECT DISTINCT d.tenant_id AS tenant_key
                    FROM definitions d
                    WHERE d.project_id IS NULL
                ) AS definition_tenants
                INNER JOIN tenants t ON t.tenant_key = definition_tenants.tenant_key
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM projects p
                    WHERE p.owner_tenant_id = t.tenant_id
                      AND p.slug = 'default');
                """);

            migrationBuilder.Sql(
                """
                UPDATE definitions d
                SET project_id = p.project_id
                FROM projects p
                INNER JOIN tenants t ON t.tenant_id = p.owner_tenant_id
                WHERE d.tenant_id = t.tenant_key
                  AND p.slug = 'default'
                  AND d.project_id IS NULL;
                """);

            migrationBuilder.DropIndex(
                name: "IX_definitions_tenant_id_slug",
                table: "definitions");

            migrationBuilder.AlterColumn<Guid>(
                name: "project_id",
                table: "definitions",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_definitions_project_id_slug",
                table: "definitions",
                columns: new[] { "project_id", "slug" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_definitions_projects_project_id",
                table: "definitions",
                column: "project_id",
                principalTable: "projects",
                principalColumn: "project_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropForeignKey(
                name: "FK_definitions_projects_project_id",
                table: "definitions");

            migrationBuilder.DropTable(
                name: "project_accesses");

            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.DropIndex(
                name: "IX_definitions_project_id_slug",
                table: "definitions");

            migrationBuilder.AlterColumn<Guid>(
                name: "project_id",
                table: "definitions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "IX_definitions_tenant_id_slug",
                table: "definitions",
                columns: new[] { "tenant_id", "slug" },
                unique: true);
        }
    }
}
