using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Statevia.Core.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowDefinitionUpdatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "workflow_definitions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE workflow_definitions SET updated_at = created_at WHERE updated_at IS NULL;");

            migrationBuilder.AlterColumn<DateTime>(
                name: "updated_at",
                table: "workflow_definitions",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            ArgumentNullException.ThrowIfNull(migrationBuilder);

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "workflow_definitions");
        }
    }
}
