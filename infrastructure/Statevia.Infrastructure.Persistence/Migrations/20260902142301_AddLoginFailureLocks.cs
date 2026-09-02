using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Statevia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginFailureLocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "login_failure_locks",
                columns: table => new
                {
                    tenant_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    locked_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_login_failure_locks", x => new { x.tenant_key, x.username });
                });

            migrationBuilder.CreateTable(
                name: "login_failure_attempts",
                columns: table => new
                {
                    attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    failed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_login_failure_attempts", x => x.attempt_id);
                    table.ForeignKey(
                        name: "FK_login_failure_attempts_login_failure_locks_tenant_key_usern~",
                        columns: x => new { x.tenant_key, x.username },
                        principalTable: "login_failure_locks",
                        principalColumns: new[] { "tenant_key", "username" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_login_failure_attempts_tenant_key_username_failed_at",
                table: "login_failure_attempts",
                columns: new[] { "tenant_key", "username", "failed_at" });

            migrationBuilder.CreateIndex(
                name: "IX_login_failure_locks_locked_until",
                table: "login_failure_locks",
                column: "locked_until");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "login_failure_attempts");

            migrationBuilder.DropTable(
                name: "login_failure_locks");
        }
    }
}
