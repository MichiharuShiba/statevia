# スキーマ定義(V2)

v2 の永続化は C# API（api/）の EF Core マイグレーションで管理。以下はレガシー TS API の SQL 参考（削除済み services/core-api の内容）。

```sql
-- executions: projection root
create table if not exists executions (
  execution_id text primary key,
  graph_id text not null,
  status text not null,
  cancel_requested_at timestamptz null,
  canceled_at timestamptz null,
  failed_at timestamptz null,
  completed_at timestamptz null,
  version int not null default 0
);

-- events: append-only log (API-owned)
create table if not exists events (
  event_id uuid primary key,
  execution_id text not null,
  seq bigserial unique,
  type text not null,
  occurred_at timestamptz not null,
  actor_kind text not null,
  actor_id text null,
  correlation_id text null,
  causation_id uuid null,
  schema_version int not null,
  payload jsonb not null
);

create index if not exists idx_events_execution_seq on events(execution_id, seq);

-- node_states: projection table
create table if not exists node_states (
  execution_id text not null,
  node_id text not null,
  node_type text not null,
  status text not null,
  attempt int not null default 0,
  worker_id text null,
  wait_key text null,
  output jsonb null,
  error jsonb null,
  canceled_by_execution boolean not null default false,
  primary key (execution_id, node_id)
);

-- command_dedup: authoritative idempotency (API-owned)
create table if not exists command_dedup (
  tenant_id text not null,
  idempotency_key text not null,
  command_fingerprint text not null,
  response_status int not null,
  response_body jsonb not null,
  created_at timestamptz not null default now(),
  primary key (tenant_id, idempotency_key)
);

create index if not exists idx_command_dedup_created_at on command_dedup(created_at);
```

- （参考）002_add_executions_version.sql

```sql
alter table executions
  add column if not exists version int not null default 0;
```

- （参考）003_add_command_dedup.sql

```sql
create table if not exists command_dedup (
  tenant_id text not null,
  idempotency_key text not null,
  command_fingerprint text not null,
  response_status int not null,
  response_body jsonb not null,
  created_at timestamptz not null default now(),
  primary key (tenant_id, idempotency_key)
);

create index if not exists idx_command_dedup_created_at on command_dedup(created_at);
```
