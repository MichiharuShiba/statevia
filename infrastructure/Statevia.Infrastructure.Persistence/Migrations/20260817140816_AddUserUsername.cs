using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Statevia.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserUsername : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_tenant_id_email",
                table: "users");

            migrationBuilder.AddColumn<string>(
                name: "username",
                table: "users",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE users
                SET username = CASE
                    WHEN position('@' IN email) = 0 THEN email
                    ELSE split_part(email, '@', 1)
                END;
                """);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM users
                        WHERE username IS NULL OR btrim(username) = ''
                    ) THEN
                        RAISE EXCEPTION 'users.username migration failed: empty username after mapping from email.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM users
                        GROUP BY tenant_id, username
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION 'users.username migration failed: duplicate local-part within a tenant. Fix rows before retrying.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM users
                        WHERE char_length(username) > 64
                           OR username !~ '^[A-Za-z0-9]([A-Za-z0-9._-]*[A-Za-z0-9])?$'
                    ) THEN
                        RAISE EXCEPTION 'users.username migration failed: username must start and end with a letter or digit; hyphen, underscore, and dot are allowed only in the middle (1–64 characters).';
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "username",
                table: "users",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320,
                oldNullable: true);

            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM users
                        WHERE email IS NOT NULL AND char_length(email) > 256
                    ) THEN
                        RAISE EXCEPTION 'users.email migration failed: email must be at most 256 characters.';
                    END IF;
                END $$;
                """);

            // @ なし識別子を NULL にする前に NOT NULL を外す。
            migrationBuilder.AlterColumn<string>(
                name: "email",
                table: "users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320);

            migrationBuilder.Sql(
                """
                UPDATE users
                SET email = NULL
                WHERE position('@' IN email) = 0;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_users_tenant_id_email",
                table: "users",
                columns: new[] { "tenant_id", "email" },
                unique: true,
                filter: "\"email\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_users_tenant_id_username",
                table: "users",
                columns: new[] { "tenant_id", "username" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_tenant_id_email",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_tenant_id_username",
                table: "users");

            migrationBuilder.Sql(
                """
                UPDATE users
                SET email = username
                WHERE email IS NULL;
                """);

            migrationBuilder.DropColumn(
                name: "username",
                table: "users");

            migrationBuilder.AlterColumn<string>(
                name: "email",
                table: "users",
                type: "character varying(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(320)",
                oldMaxLength: 320,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_tenant_id_email",
                table: "users",
                columns: new[] { "tenant_id", "email" },
                unique: true);
        }
    }
}
