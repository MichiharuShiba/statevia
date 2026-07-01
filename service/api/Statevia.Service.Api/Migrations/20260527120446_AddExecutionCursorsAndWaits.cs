using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Statevia.Service.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddExecutionCursorsAndWaits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "execution_cursors",
                columns: table => new
                {
                    execution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    current_node_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    current_runtime_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    current_worker_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    state = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_execution_cursors", x => x.execution_id);
                    table.ForeignKey(
                        name: "FK_execution_cursors_executions_execution_id",
                        column: x => x.execution_id,
                        principalTable: "executions",
                        principalColumn: "execution_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "execution_waits",
                columns: table => new
                {
                    execution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    node_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    wait_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    resume_token = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_execution_waits", x => new { x.execution_id, x.node_id });
                    table.ForeignKey(
                        name: "FK_execution_waits_executions_execution_id",
                        column: x => x.execution_id,
                        principalTable: "executions",
                        principalColumn: "execution_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_execution_waits_execution_id_resume_token",
                table: "execution_waits",
                columns: new[] { "execution_id", "resume_token" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "execution_cursors");

            migrationBuilder.DropTable(
                name: "execution_waits");
        }
    }
}
