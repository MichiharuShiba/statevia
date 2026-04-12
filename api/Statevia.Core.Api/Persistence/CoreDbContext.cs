using Microsoft.EntityFrameworkCore;

namespace Statevia.Core.Api.Persistence;

public class CoreDbContext : DbContext
{
    public CoreDbContext(DbContextOptions<CoreDbContext> options) : base(options) { }

    public DbSet<DisplayIdRow> DisplayIds => Set<DisplayIdRow>();
    public DbSet<WorkflowDefinitionRow> WorkflowDefinitions => Set<WorkflowDefinitionRow>();
    public DbSet<WorkflowRow> Workflows => Set<WorkflowRow>();
    public DbSet<EventStoreRow> EventStore => Set<EventStoreRow>();
    public DbSet<WorkflowEventRow> WorkflowEvents => Set<WorkflowEventRow>();
    public DbSet<ExecutionGraphSnapshotRow> ExecutionGraphSnapshots => Set<ExecutionGraphSnapshotRow>();
    public DbSet<CommandDedupRow> CommandDedup => Set<CommandDedupRow>();
    public DbSet<EventDeliveryDedupRow> EventDeliveryDedup => Set<EventDeliveryDedupRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // display_ids (U3): カラムはスネークケース
        modelBuilder.Entity<DisplayIdRow>(e =>
        {
            e.ToTable("display_ids");
            e.HasKey(x => new { x.Kind, x.ResourceId });
            e.HasIndex(x => x.DisplayId).IsUnique();
            e.Property(x => x.Kind).HasMaxLength(32).HasColumnName("kind");
            e.Property(x => x.DisplayId).HasMaxLength(10).HasColumnName("display_id");
            e.Property(x => x.ResourceId).HasColumnName("resource_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        // workflow_definitions
        modelBuilder.Entity<WorkflowDefinitionRow>(e =>
        {
            e.ToTable("workflow_definitions");
            e.HasKey(x => x.DefinitionId);
            e.Property(x => x.DefinitionId).HasColumnName("definition_id");
            e.Property(x => x.TenantId).HasMaxLength(64).HasColumnName("tenant_id");
            e.Property(x => x.Name).HasMaxLength(512).HasColumnName("name");
            e.Property(x => x.SourceYaml).HasColumnName("source_yaml");
            e.Property(x => x.CompiledJson).HasColumnName("compiled_json");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        // workflows (projection)
        modelBuilder.Entity<WorkflowRow>(e =>
        {
            e.ToTable("workflows");
            e.HasKey(x => x.WorkflowId);
            e.Property(x => x.WorkflowId).HasColumnName("workflow_id");
            e.Property(x => x.TenantId).HasMaxLength(64).HasColumnName("tenant_id");
            e.Property(x => x.DefinitionId).HasColumnName("definition_id");
            e.Property(x => x.Status).HasMaxLength(64).HasColumnName("status");
            e.Property(x => x.StartedAt).HasColumnName("started_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.CancelRequested).HasColumnName("cancel_requested");
            e.Property(x => x.RestartLost).HasColumnName("restart_lost");
        });

        // event_store (U2): カラムはスネークケース
        modelBuilder.Entity<EventStoreRow>(e =>
        {
            e.ToTable("event_store");
            e.HasKey(x => new { x.WorkflowId, x.Seq });
            e.HasIndex(x => x.EventId).IsUnique();
            e.Property(x => x.EventId).HasColumnName("event_id");
            e.Property(x => x.WorkflowId).HasColumnName("workflow_id");
            e.Property(x => x.Seq).HasColumnName("seq");
            e.Property(x => x.Type).HasMaxLength(128).HasColumnName("type");
            e.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            e.Property(x => x.ActorKind).HasMaxLength(32).HasColumnName("actor_kind");
            e.Property(x => x.ActorId).HasMaxLength(256).HasColumnName("actor_id");
            e.Property(x => x.CorrelationId).HasMaxLength(256).HasColumnName("correlation_id");
            e.Property(x => x.CausationId).HasColumnName("causation_id");
            e.Property(x => x.SchemaVersion).HasColumnName("schema_version");
            e.Property(x => x.PayloadJson).HasColumnName("payload_json");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        // workflow_events (監査)
        modelBuilder.Entity<WorkflowEventRow>(e =>
        {
            e.ToTable("workflow_events");
            e.HasKey(x => x.WorkflowEventId);
            e.Property(x => x.WorkflowEventId).HasColumnName("workflow_event_id");
            e.Property(x => x.WorkflowId).HasColumnName("workflow_id");
            e.Property(x => x.Seq).HasColumnName("seq");
            e.Property(x => x.Type).HasMaxLength(128).HasColumnName("type");
            e.Property(x => x.PayloadJson).HasColumnName("payload_json");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        // execution_graph_snapshots
        modelBuilder.Entity<ExecutionGraphSnapshotRow>(e =>
        {
            e.ToTable("execution_graph_snapshots");
            e.HasKey(x => x.WorkflowId);
            e.Property(x => x.WorkflowId).HasColumnName("workflow_id");
            e.Property(x => x.GraphJson).HasColumnName("graph_json");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        // command_dedup（コマンド冪等制御）
        modelBuilder.Entity<CommandDedupRow>(e =>
        {
            e.ToTable("command_dedup");
            e.HasKey(x => x.DedupKey);
            e.Property(x => x.DedupKey).HasColumnName("dedup_key");
            e.Property(x => x.Endpoint).HasColumnName("endpoint");
            e.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key");
            e.Property(x => x.RequestHash).HasColumnName("request_hash");
            e.Property(x => x.StatusCode).HasColumnName("status_code");
            e.Property(x => x.ResponseBody).HasColumnName("response_body");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");

            // TODO: ExpiresAt の DB デフォルト値（created_at + interval '24 hours'）はマイグレーションで設定する。
        });

        // event_delivery_dedup（イベント配送の冪等制御）
        modelBuilder.Entity<EventDeliveryDedupRow>(e =>
        {
            e.ToTable("event_delivery_dedup");
            e.HasKey(x => new { x.TenantId, x.WorkflowId, x.ClientEventId });
            e.Property(x => x.TenantId).HasMaxLength(64).HasColumnName("tenant_id");
            e.Property(x => x.WorkflowId).HasColumnName("workflow_id");
            e.Property(x => x.ClientEventId).HasColumnName("client_event_id");
            e.Property(x => x.BatchId).HasColumnName("batch_id");
            e.Property(x => x.Status).HasMaxLength(32).HasColumnName("status");
            e.Property(x => x.AcceptedAt).HasColumnName("accepted_at");
            e.Property(x => x.AppliedAt).HasColumnName("applied_at");
            e.Property(x => x.ErrorCode).HasMaxLength(128).HasColumnName("error_code");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            e.HasIndex(x => new { x.TenantId, x.WorkflowId, x.BatchId });
        });
    }
}
