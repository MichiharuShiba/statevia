using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Statevia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExecutionBranchContextJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "states_json",
                table: "execution_branches",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "vars_json",
                table: "execution_branches",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "states_json",
                table: "execution_branches");

            migrationBuilder.DropColumn(
                name: "vars_json",
                table: "execution_branches");
        }
    }
}
