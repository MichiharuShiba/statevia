using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Statevia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExecutionWaitsAllowedEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_execution_waits_execution_id_resume_token",
                table: "execution_waits");

            // 既存 resume_token を ["token"] へ変換してから列を入れ替える。
            migrationBuilder.Sql(
                """
                ALTER TABLE execution_waits
                    ADD COLUMN allowed_events jsonb NOT NULL DEFAULT '[]'::jsonb;

                UPDATE execution_waits
                SET allowed_events = jsonb_build_array(resume_token)
                WHERE resume_token IS NOT NULL AND btrim(resume_token) <> '';

                ALTER TABLE execution_waits
                    DROP COLUMN resume_token;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE execution_waits
                    ADD COLUMN resume_token character varying(256) NOT NULL DEFAULT '';

                UPDATE execution_waits
                SET resume_token = COALESCE(allowed_events->>0, '')
                WHERE jsonb_typeof(allowed_events) = 'array'
                  AND jsonb_array_length(allowed_events) > 0;

                ALTER TABLE execution_waits
                    DROP COLUMN allowed_events;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_execution_waits_execution_id_resume_token",
                table: "execution_waits",
                columns: new[] { "execution_id", "resume_token" });
        }
    }
}
