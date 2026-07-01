# 運用: Docker Compose（フェーズ 4.5）

リポジトリ直下の `docker-compose.yml` で **PostgreSQL 16**・**Core-API（C#）**・**UI（Next.js）** を起動できます。

## 前提

- Docker / Docker Compose が利用できること
- リポジトリルートに **`.env`** があること（初回は `cp .env.example .env`）。`POSTGRES_*` は compose の `postgres` / `service-api` で共有する
- 初回は **EF Core マイグレーション**で DB スキーマを作成する（`service-api` コンテナ起動前または起動後にホストから実行する運用が一般的）

## 起動

```bash
cp .env.example .env   # 初回のみ
docker compose up -d postgres
# マイグレーション（ホストから。DB ホスト名は localhost）
set -a && source .env && set +a
export DATABASE_URL="postgres://${POSTGRES_USER}:${POSTGRES_PASSWORD}@localhost:${POSTGRES_PORT}/${POSTGRES_DB}"
cd service/api && dotnet ef database update --project Statevia.Service.Api
docker compose up -d
```

- **PostgreSQL**: `localhost:5432`（ユーザー/DB: `statevia` / `statevia`）
- **Core-API**: `http://localhost:8080`（`DATABASE_URL` は compose 内で `POSTGRES_*` から組み立て。ホストからの `ef` は `.env` または手元の URL）
- **Action Host**（gRPC）: `http://localhost:5001`（`STATEVIA_MODULES_PATH=/app/modules`。task 14 以降 Core-API から OutOfProcess 実行に利用）
- **UI**: `http://localhost:3000`（`CORE_API_INTERNAL_BASE=http://service-api:8080`）

## ヘルス

- Core-API: `GET http://localhost:8080/v1/health` → `{ "status": "ok" }`

## API ドキュメント（OpenAPI / Scalar）

`docker compose` の **service-api** は `ASPNETCORE_ENVIRONMENT=Development` のため、次にアクセスできます。

- Scalar UI: `http://localhost:8080/scalar/v1`
- OpenAPI JSON: `http://localhost:8080/swagger/v1/swagger.json`

**本番向けに service-api イメージだけ**を Production で動かす場合は、上記は **既定で無効**です。意図的に有効化する場合は `STATEVIA_ENABLE_API_DOCS=true` を設定してください（API 構造の露出に注意）。

## トラブルシュート

- compose のサービス名を変更したあと `port is already allocated` や `Found orphan containers` が出る → 旧コンテナ（例: `statevia-core-api-1`, `statevia-ui-1`）がポートを占有している。`docker compose up -d --remove-orphans` で orphan を削除するか、`docker compose down --remove-orphans` のあと再起動する
- マイグレーション未適用で API が失敗する → `database update` を実行してから `service-api` を再起動
- UI から API に届かない → compose では `ui-studio` → `service-api` は内部 DNS 名。ブラウザからは UI のプロキシ（`/api/core/...`）経由でアクセス

## 環境変数（参照）

| サービス   | 主な変数 |
| ---------- | -------- |
| service-api | `DATABASE_URL`（`POSTGRES_*` から組み立て）, `ASPNETCORE_URLS`, `ASPNETCORE_ENVIRONMENT`（compose では `Development`） |
| postgres    | `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB`（`.env` / `.env.example`） |
| action-host | `ASPNETCORE_URLS`（compose では `http://+:5001`）, `STATEVIA_MODULES_PATH`（`/app/modules`） |
| ui-studio  | `CORE_API_INTERNAL_BASE` |

詳細は `docker-compose.yml` と `AGENTS.md` を参照してください。

## Action Module（plugins）

Core-API は起動時に **modules ルート**（`STATEVIA_MODULES_PATH` または `Statevia:Modules:Path`、未設定時は `{ContentRoot}/modules`）を scan して Action Module を load する。更新・削除の反映は **再起動**または **明示 reload** が必要（add-only watcher は新規追加のみ）。

**Action Host**（`docker compose` の `action-host` サービス）は同じ `./modules` ボリュームをマウントし、**別プロセス**で Module を ALC load して gRPC 実行する。現時点では Core-API の InProcess 経路が既定で、OutOfProcess 連携（task 14）は未接続。

### 配置（CLI）

```bash
dotnet build cli/statevia-cli.sln
dotnet run --project cli/Statevia.Service.Cli -- module install ./my-module.zip \
  --modules-path ./modules \
  --api-base http://localhost:8080 \
  --token "<tenant-admin-jwt>"
```

- `--skip-reload` で filesystem 配置のみ。
- reload にはテナント管理者の Bearer JWT と `X-Tenant-Id`（既定 `default`）が必要。
- **セキュリティ**: modules ルートへの書き込みは運用者・デプロイの信頼境界とする（テナントが HTTP から任意配置できない）。

### 運用確認

- `GET /v1/admin/modules`（テナント管理者 JWT）で load 状態一覧。
- `POST /internal/modules/reload` で再 scan / load。

詳細は `docs/statevia-directory.md` と `docs/action-module-zip-layout.md` を参照。
