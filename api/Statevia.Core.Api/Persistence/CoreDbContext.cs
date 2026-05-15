using Microsoft.EntityFrameworkCore;

namespace Statevia.Core.Api.Persistence;

internal class CoreDbContext : DbContext
{
    private static class Columns
    {
        public const string CreatedAt = "created_at";
        public const string UpdatedAt = "updated_at";
        public const string TenantId = "tenant_id";
        public const string WorkflowId = "workflow_id";
        public const string DefinitionId = "definition_id";
        public const string DisplayId = "display_id";
        public const string ResourceId = "resource_id";
        public const string Kind = "kind";
        public const string Name = "name";
        public const string SourceYaml = "source_yaml";
        public const string CompiledJson = "compiled_json";
        public const string Status = "status";
        public const string StartedAt = "started_at";
        public const string CancelRequested = "cancel_requested";
        public const string RestartLost = "restart_lost";
        public const string EventId = "event_id";
        public const string Seq = "seq";
        public const string Type = "type";
        public const string OccurredAt = "occurred_at";
        public const string ActorKind = "actor_kind";
        public const string ActorId = "actor_id";
        public const string CorrelationId = "correlation_id";
        public const string CausationId = "causation_id";
        public const string SchemaVersion = "schema_version";
        public const string PayloadJson = "payload_json";
        public const string WorkflowEventId = "workflow_event_id";
        public const string GraphJson = "graph_json";
        public const string DedupKey = "dedup_key";
        public const string Endpoint = "endpoint";
        public const string IdempotencyKey = "idempotency_key";
        public const string RequestHash = "request_hash";
        public const string StatusCode = "status_code";
        public const string ResponseBody = "response_body";
        public const string ExpiresAt = "expires_at";
        public const string ClientEventId = "client_event_id";
        public const string BatchId = "batch_id";
        public const string AcceptedAt = "accepted_at";
        public const string AppliedAt = "applied_at";
        public const string ErrorCode = "error_code";
    }

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
            e.Property(x => x.Kind).HasMaxLength(32).HasColumnName(Columns.Kind);
            e.Property(x => x.DisplayId).HasMaxLength(10).HasColumnName(Columns.DisplayId);
            e.Property(x => x.ResourceId).HasColumnName(Columns.ResourceId);
            e.Property(x => x.CreatedAt).HasColumnName(Columns.CreatedAt);
        });

        // workflow_definitions
        modelBuilder.Entity<WorkflowDefinitionRow>(e =>
        {
            e.ToTable("workflow_definitions");
            e.HasKey(x => x.DefinitionId);
            e.Property(x => x.DefinitionId).HasColumnName(Columns.DefinitionId);
            e.Property(x => x.TenantId).HasMaxLength(64).HasColumnName(Columns.TenantId);
            e.Property(x => x.Name).HasMaxLength(512).HasColumnName(Columns.Name);
            e.Property(x => x.SourceYaml).HasColumnName(Columns.SourceYaml);
            e.Property(x => x.CompiledJson).HasColumnName(Columns.CompiledJson);
            e.Property(x => x.CreatedAt).HasColumnName(Columns.CreatedAt);
            e.Property(x => x.UpdatedAt).HasColumnName(Columns.UpdatedAt);
        });

        // workflows (projection)
        modelBuilder.Entity<WorkflowRow>(e =>
        {
            e.ToTable("workflows");
            e.HasKey(x => x.WorkflowId);
            e.Property(x => x.WorkflowId).HasColumnName(Columns.WorkflowId);
            e.Property(x => x.TenantId).HasMaxLength(64).HasColumnName(Columns.TenantId);
            e.Property(x => x.DefinitionId).HasColumnName(Columns.DefinitionId);
            e.Property(x => x.Status).HasMaxLength(64).HasColumnName(Columns.Status);
            e.Property(x => x.StartedAt).HasColumnName(Columns.StartedAt);
            e.Property(x => x.UpdatedAt).HasColumnName(Columns.UpdatedAt);
            e.Property(x => x.CancelRequested).HasColumnName(Columns.CancelRequested);
            e.Property(x => x.RestartLost).HasColumnName(Columns.RestartLost);
        });

        // event_store (U2): カラムはスネークケース
        modelBuilder.Entity<EventStoreRow>(e =>
        {
            e.ToTable("event_store");
            e.HasKey(x => new { x.WorkflowId, x.Seq });
            e.HasIndex(x => x.EventId).IsUnique();
            e.Property(x => x.EventId).HasColumnName(Columns.EventId);
            e.Property(x => x.WorkflowId).HasColumnName(Columns.WorkflowId);
            e.Property(x => x.Seq).HasColumnName(Columns.Seq);
            e.Property(x => x.Type).HasMaxLength(128).HasColumnName(Columns.Type);
            e.Property(x => x.OccurredAt).HasColumnName(Columns.OccurredAt);
            e.Property(x => x.ActorKind).HasMaxLength(32).HasColumnName(Columns.ActorKind);
            e.Property(x => x.ActorId).HasMaxLength(256).HasColumnName(Columns.ActorId);
            e.Property(x => x.CorrelationId).HasMaxLength(256).HasColumnName(Columns.CorrelationId);
            e.Property(x => x.CausationId).HasColumnName(Columns.CausationId);
            e.Property(x => x.SchemaVersion).HasColumnName(Columns.SchemaVersion);
            e.Property(x => x.PayloadJson).HasColumnName(Columns.PayloadJson);
            e.Property(x => x.CreatedAt).HasColumnName(Columns.CreatedAt);
        });

        // workflow_events (監査)
        modelBuilder.Entity<WorkflowEventRow>(e =>
        {
            e.ToTable("workflow_events");
            e.HasKey(x => x.WorkflowEventId);
            e.Property(x => x.WorkflowEventId).HasColumnName(Columns.WorkflowEventId);
            e.Property(x => x.WorkflowId).HasColumnName(Columns.WorkflowId);
            e.Property(x => x.Seq).HasColumnName(Columns.Seq);
            e.Property(x => x.Type).HasMaxLength(128).HasColumnName(Columns.Type);
            e.Property(x => x.PayloadJson).HasColumnName(Columns.PayloadJson);
            e.Property(x => x.CreatedAt).HasColumnName(Columns.CreatedAt);
        });

        // execution_graph_snapshots
        modelBuilder.Entity<ExecutionGraphSnapshotRow>(e =>
        {
            e.ToTable("execution_graph_snapshots");
            e.HasKey(x => x.WorkflowId);
            e.Property(x => x.WorkflowId).HasColumnName(Columns.WorkflowId);
            e.Property(x => x.GraphJson).HasColumnName(Columns.GraphJson);
            e.Property(x => x.UpdatedAt).HasColumnName(Columns.UpdatedAt);
        });

        // command_dedup（コマンド冪等制御）
        modelBuilder.Entity<CommandDedupRow>(e =>
        {
            e.ToTable("command_dedup");
            e.HasKey(x => x.DedupKey);
            e.Property(x => x.DedupKey).HasColumnName(Columns.DedupKey);
            e.Property(x => x.Endpoint).HasColumnName(Columns.Endpoint);
            e.Property(x => x.IdempotencyKey).HasColumnName(Columns.IdempotencyKey);
            e.Property(x => x.RequestHash).HasColumnName(Columns.RequestHash);
            e.Property(x => x.StatusCode).HasColumnName(Columns.StatusCode);
            e.Property(x => x.ResponseBody).HasColumnName(Columns.ResponseBody);
            e.Property(x => x.CreatedAt).HasColumnName(Columns.CreatedAt);
            e.Property(x => x.ExpiresAt).HasColumnName(Columns.ExpiresAt);

            // TODO: ExpiresAt の DB デフォルト値（created_at + interval '24 hours'）はマイグレーションで設定する。
        });

        // event_delivery_dedup（イベント配送の冪等制御）
        modelBuilder.Entity<EventDeliveryDedupRow>(e =>
        {
            e.ToTable("event_delivery_dedup");
            e.HasKey(x => new { x.TenantId, x.WorkflowId, x.ClientEventId });
            e.Property(x => x.TenantId).HasMaxLength(64).HasColumnName(Columns.TenantId);
            e.Property(x => x.WorkflowId).HasColumnName(Columns.WorkflowId);
            e.Property(x => x.ClientEventId).HasColumnName(Columns.ClientEventId);
            e.Property(x => x.BatchId).HasColumnName(Columns.BatchId);
            e.Property(x => x.Status).HasMaxLength(32).HasColumnName(Columns.Status);
            e.Property(x => x.AcceptedAt).HasColumnName(Columns.AcceptedAt);
            e.Property(x => x.AppliedAt).HasColumnName(Columns.AppliedAt);
            e.Property(x => x.ErrorCode).HasMaxLength(128).HasColumnName(Columns.ErrorCode);
            e.Property(x => x.UpdatedAt).HasColumnName(Columns.UpdatedAt);

            e.HasIndex(x => new { x.TenantId, x.WorkflowId, x.BatchId });
        });
    }
}
