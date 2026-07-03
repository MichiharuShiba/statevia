using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Statevia.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class ExecutionTenantIdUuidFk : Migration
{
    private static readonly string[] TenantScopedTables =
    [
        "definitions",
        "executions",
        "execution_cursors",
        "workflow_definitions"
    ];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var table in TenantScopedTables)
        {
            BackfillTenantIdFromTenantKey(migrationBuilder, table);
            CreateTenantForeignKey(migrationBuilder, table);
        }

        MigrateEventDeliveryDedupTenantId(migrationBuilder);
        migrationBuilder.AddForeignKey(
            name: "FK_event_delivery_dedup_tenants_tenant_id",
            table: "event_delivery_dedup",
            column: "tenant_id",
            principalTable: "tenants",
            principalColumn: "tenant_id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Down は tenant_key 文字列へ戻す。tenants に存在しない UUID が残っている場合は失敗する。
    /// 本番で Down 不能の場合はバックアップから tenant_id (varchar) 列を手動復旧すること。
    /// </remarks>
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_event_delivery_dedup_tenants_tenant_id",
            table: "event_delivery_dedup");

        RestoreEventDeliveryDedupTenantKey(migrationBuilder);

        for (var index = TenantScopedTables.Length - 1; index >= 0; index--)
        {
            var table = TenantScopedTables[index];
            migrationBuilder.DropForeignKey(
                name: $"FK_{table}_tenants_tenant_id",
                table: table);
            migrationBuilder.DropIndex(
                name: $"IX_{table}_tenant_id",
                table: table);
            RestoreTenantKeyColumn(migrationBuilder, table);
        }
    }

    private static void BackfillTenantIdFromTenantKey(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "tenant_id_uuid",
            table: table,
            type: "uuid",
            nullable: true);

        migrationBuilder.Sql(
            $"""
             UPDATE {table} AS target
             SET tenant_id_uuid = tenant.tenant_id
             FROM tenants AS tenant
             WHERE target.tenant_id = tenant.tenant_key;
             """);

        migrationBuilder.Sql(
            $"""
             DO $$
             BEGIN
               IF EXISTS (SELECT 1 FROM {table} WHERE tenant_id_uuid IS NULL) THEN
                 RAISE EXCEPTION 'tenant_id backfill failed on {table}: unknown tenant_key (no matching tenants.tenant_key)';
               END IF;
             END $$;
             """);

        migrationBuilder.DropColumn(
            name: "tenant_id",
            table: table);

        migrationBuilder.RenameColumn(
            name: "tenant_id_uuid",
            table: table,
            newName: "tenant_id");

        migrationBuilder.AlterColumn<Guid>(
            name: "tenant_id",
            table: table,
            type: "uuid",
            nullable: false);
    }

    private static void CreateTenantForeignKey(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.CreateIndex(
            name: $"IX_{table}_tenant_id",
            table: table,
            column: "tenant_id");

        migrationBuilder.AddForeignKey(
            name: $"FK_{table}_tenants_tenant_id",
            table: table,
            column: "tenant_id",
            principalTable: "tenants",
            principalColumn: "tenant_id",
            onDelete: ReferentialAction.Restrict);
    }

    private static void MigrateEventDeliveryDedupTenantId(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_event_delivery_dedup_tenant_id_execution_id_batch_id",
            table: "event_delivery_dedup");

        migrationBuilder.DropPrimaryKey(
            name: "PK_event_delivery_dedup",
            table: "event_delivery_dedup");

        migrationBuilder.AddColumn<Guid>(
            name: "tenant_id_uuid",
            table: "event_delivery_dedup",
            type: "uuid",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE event_delivery_dedup AS target
            SET tenant_id_uuid = tenant.tenant_id
            FROM tenants AS tenant
            WHERE target.tenant_id = tenant.tenant_key;
            """);

        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
              IF EXISTS (SELECT 1 FROM event_delivery_dedup WHERE tenant_id_uuid IS NULL) THEN
                RAISE EXCEPTION 'tenant_id backfill failed on event_delivery_dedup: unknown tenant_key (no matching tenants.tenant_key)';
              END IF;
            END $$;
            """);

        migrationBuilder.DropColumn(
            name: "tenant_id",
            table: "event_delivery_dedup");

        migrationBuilder.RenameColumn(
            name: "tenant_id_uuid",
            table: "event_delivery_dedup",
            newName: "tenant_id");

        migrationBuilder.AlterColumn<Guid>(
            name: "tenant_id",
            table: "event_delivery_dedup",
            type: "uuid",
            nullable: false);

        migrationBuilder.AddPrimaryKey(
            name: "PK_event_delivery_dedup",
            table: "event_delivery_dedup",
            columns: ["tenant_id", "execution_id", "client_event_id"]);

        migrationBuilder.CreateIndex(
            name: "IX_event_delivery_dedup_tenant_id_execution_id_batch_id",
            table: "event_delivery_dedup",
            columns: ["tenant_id", "execution_id", "batch_id"]);
    }

    private static void RestoreTenantKeyColumn(MigrationBuilder migrationBuilder, string table)
    {
        migrationBuilder.AddColumn<string>(
            name: "tenant_id_key",
            table: table,
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.Sql(
            $"""
             UPDATE {table} AS target
             SET tenant_id_key = tenant.tenant_key
             FROM tenants AS tenant
             WHERE target.tenant_id = tenant.tenant_id;
             """);

        migrationBuilder.Sql(
            $"""
             DO $$
             BEGIN
               IF EXISTS (SELECT 1 FROM {table} WHERE tenant_id_key IS NULL) THEN
                 RAISE EXCEPTION 'tenant_id down-migration failed on {table}: tenant UUID without tenants row';
               END IF;
             END $$;
             """);

        migrationBuilder.DropColumn(
            name: "tenant_id",
            table: table);

        migrationBuilder.RenameColumn(
            name: "tenant_id_key",
            table: table,
            newName: "tenant_id");

        migrationBuilder.AlterColumn<string>(
            name: "tenant_id",
            table: table,
            type: "character varying(64)",
            maxLength: 64,
            nullable: false);
    }

    private static void RestoreEventDeliveryDedupTenantKey(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_event_delivery_dedup_tenant_id_execution_id_batch_id",
            table: "event_delivery_dedup");

        migrationBuilder.DropPrimaryKey(
            name: "PK_event_delivery_dedup",
            table: "event_delivery_dedup");

        migrationBuilder.AddColumn<string>(
            name: "tenant_id_key",
            table: "event_delivery_dedup",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE event_delivery_dedup AS target
            SET tenant_id_key = tenant.tenant_key
            FROM tenants AS tenant
            WHERE target.tenant_id = tenant.tenant_id;
            """);

        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
              IF EXISTS (SELECT 1 FROM event_delivery_dedup WHERE tenant_id_key IS NULL) THEN
                RAISE EXCEPTION 'tenant_id down-migration failed on event_delivery_dedup: tenant UUID without tenants row';
              END IF;
            END $$;
            """);

        migrationBuilder.DropColumn(
            name: "tenant_id",
            table: "event_delivery_dedup");

        migrationBuilder.RenameColumn(
            name: "tenant_id_key",
            table: "event_delivery_dedup",
            newName: "tenant_id");

        migrationBuilder.AlterColumn<string>(
            name: "tenant_id",
            table: "event_delivery_dedup",
            type: "character varying(64)",
            maxLength: 64,
            nullable: false);

        migrationBuilder.AddPrimaryKey(
            name: "PK_event_delivery_dedup",
            table: "event_delivery_dedup",
            columns: ["tenant_id", "execution_id", "client_event_id"]);

        migrationBuilder.CreateIndex(
            name: "IX_event_delivery_dedup_tenant_id_execution_id_batch_id",
            table: "event_delivery_dedup",
            columns: ["tenant_id", "execution_id", "batch_id"]);
    }
}
