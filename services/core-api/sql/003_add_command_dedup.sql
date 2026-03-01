-- command_dedup: authoritative idempotency (API-owned)
-- scheme.v2: tenant_id + idempotency_key で冪等を管理
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
