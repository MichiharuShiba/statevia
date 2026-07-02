using Microsoft.EntityFrameworkCore;
using Statevia.Core.Application.Contracts.Persistence;

namespace Statevia.Service.Api.Persistence;

internal class CoreDbContext : DbContext, ICoreDatabase
{
    private static class Columns
    {
        public const string CreatedAt = "created_at";
        public const string UpdatedAt = "updated_at";
        public const string TenantId = "tenant_id";
        public const string ExecutionId = "execution_id";
        public const string DefinitionId = "definition_id";
        public const string DefinitionVersionId = "definition_version_id";
        public const string ProjectId = "project_id";
        public const string Slug = "slug";
        public const string LatestVersion = "latest_version";
        public const string Version = "version";
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
        public const string ExecutionEventId = "execution_event_id";
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
        public const string TenantKey = "tenant_key";
        public const string DisplayName = "display_name";
        public const string Lifecycle = "lifecycle";
        public const string Email = "email";
        public const string PasswordHash = "password_hash";
        public const string IsTenantAdmin = "is_tenant_admin";
        public const string IsPlatformAdmin = "is_platform_admin";
        public const string IsActive = "is_active";
        public const string DisabledAt = "disabled_at";
        public const string IsSystem = "is_system";
        public const string PrincipalScope = "principal_scope";
        public const string PrincipalType = "principal_type";
        public const string PrincipalId = "principal_id";
        public const string UserId = "user_id";
        public const string GroupId = "group_id";
        public const string PermissionKey = "permission_key";
        public const string DisplayLabel = "display_label";
        public const string DisplayKey = "display_key";
        public const string OwnerType = "owner_type";
        public const string OwnerKey = "owner_key";
        public const string IsDeprecated = "is_deprecated";
        public const string DeletedAt = "deleted_at";
        public const string KeyPrefix = "key_prefix";
        public const string KeyHash = "key_hash";
        public const string AllowedScopesJson = "allowed_scopes_json";
        public const string LastUsedAt = "last_used_at";
        public const string PermissionDefinitionId = "permission_definition_id";
        public const string ServiceAccountId = "service_account_id";
        public const string ApiKeyId = "api_key_id";
        public const string OwnerTenantId = "owner_tenant_id";
        public const string Visibility = "visibility";
        public const string Description = "description";
        public const string Role = "role";
        public const string CurrentNodeId = "current_node_id";
        public const string CurrentRuntimeId = "current_runtime_id";
        public const string CurrentWorkerId = "current_worker_id";
        public const string State = "state";
        public const string NodeId = "node_id";
        public const string WaitKind = "wait_kind";
        public const string ResumeToken = "resume_token";
        public const string SecuritySnapshotJson = "security_snapshot_json";
    }

    private readonly ITenantContextAccessor _tenantAccessor;
    private readonly ITenantQueryFilterOptions _queryFilterOptions;

    /// <summary>新しいインスタンスを初期化する。</summary>
    public CoreDbContext(
        DbContextOptions<CoreDbContext> options,
        ITenantContextAccessor? tenantAccessor = null,
        ITenantQueryFilterOptions? queryFilterOptions = null) : base(options)
    {
        _tenantAccessor = tenantAccessor ?? NullTenantContextAccessor.Instance;
        _queryFilterOptions = queryFilterOptions ?? DisabledTenantQueryFilterOptions.Instance;
    }

    public DbSet<DisplayIdRow> DisplayIds => Set<DisplayIdRow>();
    public DbSet<WorkflowDefinitionRow> WorkflowDefinitions => Set<WorkflowDefinitionRow>();
    public DbSet<DefinitionRow> Definitions => Set<DefinitionRow>();
    public DbSet<DefinitionVersionRow> DefinitionVersions => Set<DefinitionVersionRow>();
    public DbSet<ExecutionRow> Executions => Set<ExecutionRow>();
    public DbSet<EventStoreRow> EventStore => Set<EventStoreRow>();
    public DbSet<ExecutionEventRow> ExecutionEvents => Set<ExecutionEventRow>();
    public DbSet<ExecutionGraphSnapshotRow> ExecutionGraphSnapshots => Set<ExecutionGraphSnapshotRow>();
    public DbSet<ExecutionCursorRow> ExecutionCursors => Set<ExecutionCursorRow>();
    public DbSet<ExecutionWaitRow> ExecutionWaits => Set<ExecutionWaitRow>();
    public DbSet<CommandDedupRow> CommandDedup => Set<CommandDedupRow>();
    public DbSet<EventDeliveryDedupRow> EventDeliveryDedup => Set<EventDeliveryDedupRow>();
    public DbSet<TenantRow> Tenants => Set<TenantRow>();
    public DbSet<PermissionDefinitionRow> PermissionDefinitions => Set<PermissionDefinitionRow>();
    public DbSet<PrincipalRow> Principals => Set<PrincipalRow>();
    public DbSet<UserPrincipalRow> UserPrincipals => Set<UserPrincipalRow>();
    public DbSet<UserRow> Users => Set<UserRow>();
    public DbSet<GroupRow> Groups => Set<GroupRow>();
    public DbSet<GroupPermissionRow> GroupPermissions => Set<GroupPermissionRow>();
    public DbSet<UserGroupMemberRow> UserGroupMembers => Set<UserGroupMemberRow>();
    public DbSet<ServiceAccountRow> ServiceAccounts => Set<ServiceAccountRow>();
    public DbSet<ServiceAccountGroupMemberRow> ServiceAccountGroupMembers => Set<ServiceAccountGroupMemberRow>();
    public DbSet<ApiKeyRow> ApiKeys => Set<ApiKeyRow>();
    public DbSet<ProjectRow> Projects => Set<ProjectRow>();
    public DbSet<ProjectAccessRow> ProjectAccesses => Set<ProjectAccessRow>();

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

        // workflow_definitions（レガシー。新規書き込みは definitions / definition_versions のみ）
        modelBuilder.Entity<WorkflowDefinitionRow>(e =>
        {
            e.ToTable("workflow_definitions");
            e.HasKey(x => x.DefinitionId);
            e.Property(x => x.DefinitionId).HasColumnName(Columns.DefinitionId);
            e.Property(x => x.TenantId).HasColumnName(Columns.TenantId);
            e.Property(x => x.Name).HasMaxLength(512).HasColumnName(Columns.Name);
            e.Property(x => x.SourceYaml).HasColumnName(Columns.SourceYaml);
            e.Property(x => x.CompiledJson).HasColumnName(Columns.CompiledJson);
            e.Property(x => x.CreatedAt).HasColumnName(Columns.CreatedAt);
            e.Property(x => x.UpdatedAt).HasColumnName(Columns.UpdatedAt);
            e.HasOne<TenantRow>()
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // definitions（論理定義メタ）
        modelBuilder.Entity<DefinitionRow>(e =>
        {
            e.ToTable("definitions");
            e.HasKey(x => x.DefinitionId);
            e.Property(x => x.DefinitionId).HasColumnName(Columns.DefinitionId);
            e.Property(x => x.TenantId).HasColumnName(Columns.TenantId);
            e.Property(x => x.ProjectId).HasColumnName(Columns.ProjectId);
            e.Property(x => x.Slug).HasMaxLength(128).HasColumnName(Columns.Slug);
            e.Property(x => x.Name).HasMaxLength(512).HasColumnName(Columns.Name);
            e.Property(x => x.LatestVersion).HasColumnName(Columns.LatestVersion);
            e.Property(x => x.CreatedAt).HasColumnName(Columns.CreatedAt);
            e.Property(x => x.UpdatedAt).HasColumnName(Columns.UpdatedAt);
            e.HasIndex(x => new { x.ProjectId, x.Slug }).IsUnique();
            e.HasOne<ProjectRow>()
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<TenantRow>()
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // definition_versions（immutable truth）
        modelBuilder.Entity<DefinitionVersionRow>(e =>
        {
            e.ToTable("definition_versions");
            e.HasKey(x => x.DefinitionVersionId);
            e.Property(x => x.DefinitionVersionId).HasColumnName(Columns.DefinitionVersionId);
            e.Property(x => x.DefinitionId).HasColumnName(Columns.DefinitionId);
            e.Property(x => x.Version).HasColumnName(Columns.Version);
            e.Property(x => x.SourceYaml).HasColumnName(Columns.SourceYaml);
            e.Property(x => x.CompiledJson).HasColumnName(Columns.CompiledJson);
            e.Property(x => x.CreatedAt).HasColumnName(Columns.CreatedAt);
            e.HasIndex(x => new { x.DefinitionId, x.Version }).IsUnique();
            e.HasOne<DefinitionRow>()
                .WithMany()
                .HasForeignKey(x => x.DefinitionId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // executions (projection)
        modelBuilder.Entity<ExecutionRow>(e =>
        {
            e.ToTable("executions");
            e.HasKey(x => x.ExecutionId);
            e.Property(x => x.ExecutionId).HasColumnName(Columns.ExecutionId);
            e.Property(x => x.TenantId).HasColumnName(Columns.TenantId);
            e.Property(x => x.DefinitionId).HasColumnName(Columns.DefinitionId);
            e.Property(x => x.DefinitionVersionId).HasColumnName(Columns.DefinitionVersionId);
            e.Property(x => x.Status).HasMaxLength(64).HasColumnName(Columns.Status);
            e.HasOne<DefinitionVersionRow>()
                .WithMany()
                .HasForeignKey(x => x.DefinitionVersionId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<TenantRow>()
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.StartedAt).HasColumnName(Columns.StartedAt);
            e.Property(x => x.UpdatedAt).HasColumnName(Columns.UpdatedAt);
            e.Property(x => x.CancelRequested).HasColumnName(Columns.CancelRequested);
            e.Property(x => x.RestartLost).HasColumnName(Columns.RestartLost);
            e.Property(x => x.SecuritySnapshotJson).HasColumnName(Columns.SecuritySnapshotJson);
        });

        // event_store (U2): カラムはスネークケース
        modelBuilder.Entity<EventStoreRow>(e =>
        {
            e.ToTable("event_store");
            e.HasKey(x => new { x.ExecutionId, x.Seq });
            e.HasIndex(x => x.EventId).IsUnique();
            e.Property(x => x.EventId).HasColumnName(Columns.EventId);
            e.Property(x => x.ExecutionId).HasColumnName(Columns.ExecutionId);
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

        // execution_events (監査)
        modelBuilder.Entity<ExecutionEventRow>(e =>
        {
            e.ToTable("execution_events");
            e.HasKey(x => x.ExecutionEventId);
            e.Property(x => x.ExecutionEventId).HasColumnName(Columns.ExecutionEventId);
            e.Property(x => x.ExecutionId).HasColumnName(Columns.ExecutionId);
            e.Property(x => x.Seq).HasColumnName(Columns.Seq);
            e.Property(x => x.Type).HasMaxLength(128).HasColumnName(Columns.Type);
            e.Property(x => x.PayloadJson).HasColumnName(Columns.PayloadJson);
            e.Property(x => x.CreatedAt).HasColumnName(Columns.CreatedAt);
        });

        // execution_graph_snapshots
        modelBuilder.Entity<ExecutionGraphSnapshotRow>(e =>
        {
            e.ToTable("execution_graph_snapshots");
            e.HasKey(x => x.ExecutionId);
            e.Property(x => x.ExecutionId).HasColumnName(Columns.ExecutionId);
            e.Property(x => x.GraphJson).HasColumnName(Columns.GraphJson);
            e.Property(x => x.UpdatedAt).HasColumnName(Columns.UpdatedAt);
        });

        // execution_cursors（operational projection）
        modelBuilder.Entity<ExecutionCursorRow>(e =>
        {
            e.ToTable("execution_cursors");
            e.HasKey(x => x.ExecutionId);
            e.Property(x => x.ExecutionId).HasColumnName(Columns.ExecutionId);
            e.Property(x => x.TenantId).HasColumnName(Columns.TenantId);
            e.Property(x => x.CurrentNodeId).HasMaxLength(64).HasColumnName(Columns.CurrentNodeId);
            e.Property(x => x.CurrentRuntimeId).HasMaxLength(128).HasColumnName(Columns.CurrentRuntimeId);
            e.Property(x => x.CurrentWorkerId).HasMaxLength(128).HasColumnName(Columns.CurrentWorkerId);
            e.Property(x => x.State).HasMaxLength(32).HasColumnName(Columns.State);
            e.Property(x => x.UpdatedAt).HasColumnName(Columns.UpdatedAt);

            e.HasOne<ExecutionRow>()
                .WithMany()
                .HasForeignKey(x => x.ExecutionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne<TenantRow>()
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // execution_waits（durable wait のみ）
        modelBuilder.Entity<ExecutionWaitRow>(e =>
        {
            e.ToTable("execution_waits");
            e.HasKey(x => new { x.ExecutionId, x.NodeId });
            e.Property(x => x.ExecutionId).HasColumnName(Columns.ExecutionId);
            e.Property(x => x.NodeId).HasMaxLength(64).HasColumnName(Columns.NodeId);
            e.Property(x => x.WaitKind).HasConversion<string>().HasMaxLength(32).HasColumnName(Columns.WaitKind);
            e.Property(x => x.ResumeToken).HasMaxLength(256).HasColumnName(Columns.ResumeToken);
            e.Property(x => x.ExpiresAt).HasColumnName(Columns.ExpiresAt);
            e.Property(x => x.CreatedAt).HasColumnName(Columns.CreatedAt);

            e.HasIndex(x => new { x.ExecutionId, x.ResumeToken });

            e.HasOne<ExecutionRow>()
                .WithMany()
                .HasForeignKey(x => x.ExecutionId)
                .OnDelete(DeleteBehavior.Cascade);
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

            // Note: ExpiresAt の DB デフォルト値（created_at + interval '24 hours'）はマイグレーションで設定する。
        });

        // event_delivery_dedup（イベント配送の冪等制御）
        modelBuilder.Entity<EventDeliveryDedupRow>(e =>
        {
            e.ToTable("event_delivery_dedup");
            e.HasKey(x => new { x.TenantId, x.ExecutionId, x.ClientEventId });
            e.Property(x => x.TenantId).HasColumnName(Columns.TenantId);
            e.Property(x => x.ExecutionId).HasColumnName(Columns.ExecutionId);
            e.Property(x => x.ClientEventId).HasColumnName(Columns.ClientEventId);
            e.Property(x => x.BatchId).HasColumnName(Columns.BatchId);
            e.Property(x => x.Status).HasMaxLength(32).HasColumnName(Columns.Status);
            e.Property(x => x.AcceptedAt).HasColumnName(Columns.AcceptedAt);
            e.Property(x => x.AppliedAt).HasColumnName(Columns.AppliedAt);
            e.Property(x => x.ErrorCode).HasMaxLength(128).HasColumnName(Columns.ErrorCode);
            e.Property(x => x.UpdatedAt).HasColumnName(Columns.UpdatedAt);

            e.HasIndex(x => new { x.TenantId, x.ExecutionId, x.BatchId });
            e.HasOne<TenantRow>()
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        ConfigureTenantScopedFilters(modelBuilder);
        ConfigureSecurityEntities(modelBuilder);
    }

    /// <summary>
    /// テナントスコープ行は <see cref="ITenantContext.TenantId"/> のみで fail-closed フィルタする。
    /// </summary>
    private void ConfigureTenantScopedFilters(ModelBuilder modelBuilder) =>
        ConfigureTenantIdEntityFilters(modelBuilder);

    private void ConfigureTenantIdEntityFilters(ModelBuilder modelBuilder)
    {
        ConfigureCoreTenantIdEntityFilters(modelBuilder);
        ConfigureSecurityTenantIdEntityFilters(modelBuilder);
    }

    private void ConfigureCoreTenantIdEntityFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantRow>().HasQueryFilter(e =>
            !_queryFilterOptions.IsEnabled ||
            (_tenantAccessor.IsResolved && e.TenantId == _tenantAccessor.TenantId));

        modelBuilder.Entity<WorkflowDefinitionRow>().HasQueryFilter(e =>
            !_queryFilterOptions.IsEnabled ||
            (_tenantAccessor.IsResolved && e.TenantId == _tenantAccessor.TenantId));

        modelBuilder.Entity<DefinitionRow>().HasQueryFilter(e =>
            !_queryFilterOptions.IsEnabled ||
            (_tenantAccessor.IsResolved && e.TenantId == _tenantAccessor.TenantId));

        modelBuilder.Entity<ExecutionRow>().HasQueryFilter(e =>
            !_queryFilterOptions.IsEnabled ||
            (_tenantAccessor.IsResolved && e.TenantId == _tenantAccessor.TenantId));

        modelBuilder.Entity<ExecutionCursorRow>().HasQueryFilter(e =>
            !_queryFilterOptions.IsEnabled ||
            (_tenantAccessor.IsResolved && e.TenantId == _tenantAccessor.TenantId));

        modelBuilder.Entity<EventDeliveryDedupRow>().HasQueryFilter(e =>
            !_queryFilterOptions.IsEnabled ||
            (_tenantAccessor.IsResolved && e.TenantId == _tenantAccessor.TenantId));
    }

    private void ConfigureSecurityTenantIdEntityFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PrincipalRow>().HasQueryFilter(e =>
            !_queryFilterOptions.IsEnabled ||
            (_tenantAccessor.IsResolved && e.TenantId == _tenantAccessor.TenantId));

        modelBuilder.Entity<UserRow>().HasQueryFilter(e =>
            !_queryFilterOptions.IsEnabled ||
            (_tenantAccessor.IsResolved && e.TenantId == _tenantAccessor.TenantId));

        modelBuilder.Entity<GroupRow>().HasQueryFilter(e =>
            !_queryFilterOptions.IsEnabled ||
            (_tenantAccessor.IsResolved && e.TenantId == _tenantAccessor.TenantId));

        modelBuilder.Entity<ServiceAccountRow>().HasQueryFilter(e =>
            !_queryFilterOptions.IsEnabled ||
            (_tenantAccessor.IsResolved && e.TenantId == _tenantAccessor.TenantId));

        modelBuilder.Entity<ApiKeyRow>().HasQueryFilter(e =>
            !_queryFilterOptions.IsEnabled ||
            (_tenantAccessor.IsResolved && e.TenantId == _tenantAccessor.TenantId));

        modelBuilder.Entity<ProjectAccessRow>().HasQueryFilter(e =>
            !_queryFilterOptions.IsEnabled ||
            (_tenantAccessor.IsResolved && e.TenantId == _tenantAccessor.TenantId));
    }

    private static void ConfigureSecurityEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantRow>(e =>
        {
            e.ToTable("tenants");
            e.HasKey(x => x.TenantId);
            e.Property(x => x.TenantId).HasColumnName(Columns.TenantId);
            e.Property(x => x.TenantKey).HasMaxLength(64).HasColumnName(Columns.TenantKey);
            e.HasIndex(x => x.TenantKey).IsUnique();
            e.Property(x => x.DisplayName).HasMaxLength(256).HasColumnName(Columns.DisplayName);
            e.Property(x => x.Lifecycle).HasConversion<string>().HasMaxLength(32).HasColumnName(Columns.Lifecycle);
            e.Property(x => x.CreatedAt).HasColumnName(Columns.CreatedAt);
            e.Property(x => x.UpdatedAt).HasColumnName(Columns.UpdatedAt);
        });

        modelBuilder.Entity<PermissionDefinitionRow>(e =>
        {
            e.ToTable("permission_definitions");
            e.HasKey(x => x.PermissionDefinitionId);
            e.Property(x => x.PermissionDefinitionId).HasColumnName(Columns.PermissionDefinitionId);
            e.Property(x => x.PermissionKey).HasMaxLength(128).HasColumnName(Columns.PermissionKey);
            e.HasIndex(x => x.PermissionKey).IsUnique();
            e.Property(x => x.DisplayLabel).HasMaxLength(256).HasColumnName(Columns.DisplayLabel);
            e.Property(x => x.DisplayKey).HasMaxLength(128).HasColumnName(Columns.DisplayKey);
            e.Property(x => x.OwnerType).HasMaxLength(64).HasColumnName(Columns.OwnerType);
            e.Property(x => x.OwnerKey).HasMaxLength(128).HasColumnName(Columns.OwnerKey);
            e.Property(x => x.IsSystem).HasColumnName(Columns.IsSystem);
            e.Property(x => x.IsDeprecated).HasColumnName(Columns.IsDeprecated);
            e.Property(x => x.CreatedAt).HasColumnName(Columns.CreatedAt);
        });

        modelBuilder.Entity<PrincipalRow>(e =>
        {
            e.ToTable("principals");
            e.HasKey(x => x.PrincipalId);
            e.Property(x => x.PrincipalId).HasColumnName(Columns.PrincipalId);
            e.Property(x => x.TenantId).HasColumnName(Columns.TenantId);
            e.Property(x => x.PrincipalScope).HasConversion<string>().HasMaxLength(32).HasColumnName(Columns.PrincipalScope);
            e.Property(x => x.PrincipalType).HasConversion<string>().HasMaxLength(32).HasColumnName(Columns.PrincipalType);
            e.Property(x => x.DisplayName).HasMaxLength(256).HasColumnName(Columns.DisplayName);
            e.Property(x => x.IsSystem).HasColumnName(Columns.IsSystem);
            e.Property(x => x.IsActive).HasColumnName(Columns.IsActive);
            e.Property(x => x.DisabledAt).HasColumnName(Columns.DisabledAt);
            e.Property(x => x.DeletedAt).HasColumnName(Columns.DeletedAt);
            e.Property(x => x.CreatedAt).HasColumnName(Columns.CreatedAt);
            e.Property(x => x.UpdatedAt).HasColumnName(Columns.UpdatedAt);
        });

        modelBuilder.Entity<UserPrincipalRow>(e =>
        {
            e.ToTable("user_principals");
            e.HasKey(x => x.PrincipalId);
            e.Property(x => x.PrincipalId).HasColumnName(Columns.PrincipalId);
            e.Property(x => x.UserId).HasColumnName(Columns.UserId);
        });

        modelBuilder.Entity<UserRow>(e =>
        {
            e.ToTable("users");
            e.HasKey(x => x.UserId);
            e.Property(x => x.UserId).HasColumnName(Columns.UserId);
            e.Property(x => x.TenantId).HasColumnName(Columns.TenantId);
            e.Property(x => x.Email).HasMaxLength(320).HasColumnName(Columns.Email);
            e.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
            e.Property(x => x.PasswordHash).HasColumnName(Columns.PasswordHash);
            e.Property(x => x.IsTenantAdmin).HasColumnName(Columns.IsTenantAdmin);
            e.Property(x => x.IsPlatformAdmin).HasColumnName(Columns.IsPlatformAdmin);
            e.Property(x => x.IsActive).HasColumnName(Columns.IsActive);
            e.Property(x => x.DisabledAt).HasColumnName(Columns.DisabledAt);
            e.Property(x => x.CreatedAt).HasColumnName(Columns.CreatedAt);
            e.Property(x => x.UpdatedAt).HasColumnName(Columns.UpdatedAt);
        });

        modelBuilder.Entity<GroupRow>(e =>
        {
            e.ToTable("groups");
            e.HasKey(x => x.GroupId);
            e.Property(x => x.GroupId).HasColumnName(Columns.GroupId);
            e.Property(x => x.TenantId).HasColumnName(Columns.TenantId);
            e.Property(x => x.Name).HasMaxLength(128).HasColumnName(Columns.Name);
            e.Property(x => x.IsSystem).HasColumnName(Columns.IsSystem);
            e.Property(x => x.CreatedAt).HasColumnName(Columns.CreatedAt);
            e.Property(x => x.UpdatedAt).HasColumnName(Columns.UpdatedAt);
            e.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        });

        modelBuilder.Entity<GroupPermissionRow>(e =>
        {
            e.ToTable("group_permissions");
            e.HasKey(x => new { x.GroupId, x.PermissionKey });
            e.Property(x => x.GroupId).HasColumnName(Columns.GroupId);
            e.Property(x => x.PermissionKey).HasMaxLength(128).HasColumnName(Columns.PermissionKey);
        });

        modelBuilder.Entity<UserGroupMemberRow>(e =>
        {
            e.ToTable("user_group_members");
            e.HasKey(x => new { x.UserId, x.GroupId });
            e.Property(x => x.UserId).HasColumnName(Columns.UserId);
            e.Property(x => x.GroupId).HasColumnName(Columns.GroupId);
        });

        modelBuilder.Entity<ServiceAccountRow>(e =>
        {
            e.ToTable("service_accounts");
            e.HasKey(x => x.ServiceAccountId);
            e.Property(x => x.ServiceAccountId).HasColumnName(Columns.ServiceAccountId);
            e.Property(x => x.TenantId).HasColumnName(Columns.TenantId);
            e.Property(x => x.PrincipalId).HasColumnName(Columns.PrincipalId);
            e.Property(x => x.Name).HasMaxLength(128).HasColumnName(Columns.Name);
            e.Property(x => x.CreatedAt).HasColumnName(Columns.CreatedAt);
        });

        modelBuilder.Entity<ServiceAccountGroupMemberRow>(e =>
        {
            e.ToTable("service_account_group_members");
            e.HasKey(x => new { x.ServiceAccountId, x.GroupId });
            e.Property(x => x.ServiceAccountId).HasColumnName(Columns.ServiceAccountId);
            e.Property(x => x.GroupId).HasColumnName(Columns.GroupId);
        });

        modelBuilder.Entity<ApiKeyRow>(e =>
        {
            e.ToTable("api_keys");
            e.HasKey(x => x.ApiKeyId);
            e.Property(x => x.ApiKeyId).HasColumnName(Columns.ApiKeyId);
            e.Property(x => x.TenantId).HasColumnName(Columns.TenantId);
            e.Property(x => x.PrincipalId).HasColumnName(Columns.PrincipalId);
            e.Property(x => x.KeyPrefix).HasMaxLength(16).HasColumnName(Columns.KeyPrefix);
            e.Property(x => x.KeyHash).HasMaxLength(128).HasColumnName(Columns.KeyHash);
            e.Property(x => x.AllowedScopesJson).HasColumnName(Columns.AllowedScopesJson);
            e.Property(x => x.ExpiresAt).HasColumnName(Columns.ExpiresAt);
            e.Property(x => x.LastUsedAt).HasColumnName(Columns.LastUsedAt);
            e.Property(x => x.CreatedAt).HasColumnName(Columns.CreatedAt);
            e.HasIndex(x => new { x.TenantId, x.KeyPrefix });
        });

        modelBuilder.Entity<ProjectRow>(e =>
        {
            e.ToTable("projects");
            e.HasKey(x => x.ProjectId);
            e.Property(x => x.ProjectId).HasColumnName(Columns.ProjectId);
            e.Property(x => x.OwnerTenantId).HasColumnName(Columns.OwnerTenantId);
            e.Property(x => x.Slug).HasMaxLength(128).HasColumnName(Columns.Slug);
            e.Property(x => x.DisplayName).HasMaxLength(256).HasColumnName(Columns.DisplayName);
            e.Property(x => x.Visibility).HasConversion<string>().HasMaxLength(32).HasColumnName(Columns.Visibility);
            e.Property(x => x.Description).HasColumnName(Columns.Description);
            e.Property(x => x.CreatedAt).HasColumnName(Columns.CreatedAt);
            e.HasIndex(x => new { x.OwnerTenantId, x.Slug }).IsUnique();
            e.HasOne<TenantRow>()
                .WithMany()
                .HasForeignKey(x => x.OwnerTenantId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ProjectAccessRow>(e =>
        {
            e.ToTable("project_accesses");
            e.HasKey(x => new { x.ProjectId, x.TenantId });
            e.Property(x => x.ProjectId).HasColumnName(Columns.ProjectId);
            e.Property(x => x.TenantId).HasColumnName(Columns.TenantId);
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(32).HasColumnName(Columns.Role);
            e.Property(x => x.CreatedAt).HasColumnName(Columns.CreatedAt);
            e.HasOne<ProjectRow>()
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne<TenantRow>()
                .WithMany()
                .HasForeignKey(x => x.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
