using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Statevia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExecutionBranches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "execution_branches",
                columns: table => new
                {
                    parent_execution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fork_node_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    branch_state = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    execution_id = table.Column<Guid>(type: "uuid", nullable: false),
                    join_state = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    output_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_execution_branches", x => new { x.parent_execution_id, x.fork_node_id, x.branch_state });
                    table.ForeignKey(
                        name: "FK_execution_branches_executions_execution_id",
                        column: x => x.execution_id,
                        principalTable: "executions",
                        principalColumn: "execution_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_execution_branches_executions_parent_execution_id",
                        column: x => x.parent_execution_id,
                        principalTable: "executions",
                        principalColumn: "execution_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_execution_branches_execution_id",
                table: "execution_branches",
                column: "execution_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_execution_branches_parent_execution_id",
                table: "execution_branches",
                column: "parent_execution_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "execution_branches");
        }
    }
}
