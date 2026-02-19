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

create table if not exists idempotency_keys (
  idempotency_key text not null,
  endpoint text not null,
  request_hash text not null,
  response_status int not null,
  response_body jsonb not null,
  created_at timestamptz not null default now(),
  primary key (idempotency_key, endpoint)
);

-- 簡易投影（ノード状態）
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