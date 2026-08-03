using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Statevia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExecutionCheckpointOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "lease_until",
                table: "execution_runtime_checkpoints",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "owner_generation",
                table: "execution_runtime_checkpoints",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "owner_worker_id",
                table: "execution_runtime_checkpoints",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_execution_runtime_checkpoints_lease_until",
                table: "execution_runtime_checkpoints",
                column: "lease_until");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_execution_runtime_checkpoints_lease_until",
                table: "execution_runtime_checkpoints");

            migrationBuilder.DropColumn(
                name: "lease_until",
                table: "execution_runtime_checkpoints");

            migrationBuilder.DropColumn(
                name: "owner_generation",
                table: "execution_runtime_checkpoints");

            migrationBuilder.DropColumn(
                name: "owner_worker_id",
                table: "execution_runtime_checkpoints");
        }
    }
}
