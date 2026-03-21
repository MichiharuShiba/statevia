# 運用: Docker Compose（フェーズ 4.5）

リポジトリ直下の `docker-compose.yml` で **PostgreSQL 16**・**Core-API（C#）**・**UI（Next.js）** を起動できます。

## 前提

- Docker / Docker Compose が利用できること
- 初回は **EF Core マイグレーション**で DB スキーマを作成する（`core-api` コンテナ起動前または起動後にホストから実行する運用が一般的）

## 起動

```bash
docker compose up -d postgres
# マイグレーション（例: ホストに .NET SDK がある場合）
cd api && dotnet ef database update --project Statevia.Core.Api
docker compose up -d
```

- **PostgreSQL**: `localhost:5432`（ユーザー/DB: `statevia` / `statevia`）
- **Core-API**: `http://localhost:8080`（`DATABASE_URL` は compose 内で `postgres` サービス向けに設定済み）
- **UI**: `http://localhost:3000`（`CORE_API_INTERNAL_BASE=http://core-api:8080`）

## ヘルス

- Core-API: `GET http://localhost:8080/v1/health` → `{ "status": "ok" }`

## トラブルシュート

- マイグレーション未適用で API が失敗する → `database update` を実行してから `core-api` を再起動
- UI から API に届かない → compose では `ui` → `core-api` は内部 DNS 名。ブラウザからは UI のプロキシ（`/api/core/...`）経由でアクセス

## 環境変数（参照）

| サービス   | 主な変数 |
| ---------- | -------- |
| core-api   | `DATABASE_URL`, `ASPNETCORE_URLS` |
| ui         | `CORE_API_INTERNAL_BASE` |

詳細は `docker-compose.yml` と `AGENTS.md` を参照してください。
