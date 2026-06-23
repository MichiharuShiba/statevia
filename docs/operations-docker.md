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

## API ドキュメント（OpenAPI / Scalar）

`docker compose` の **core-api** は `ASPNETCORE_ENVIRONMENT=Development` のため、次にアクセスできます。

- Scalar UI: `http://localhost:8080/scalar/v1`
- OpenAPI JSON: `http://localhost:8080/swagger/v1/swagger.json`

**本番向けに core-api イメージだけ**を Production で動かす場合は、上記は **既定で無効**です。意図的に有効化する場合は `STATEVIA_ENABLE_API_DOCS=true` を設定してください（API 構造の露出に注意）。

## トラブルシュート

- マイグレーション未適用で API が失敗する → `database update` を実行してから `core-api` を再起動
- UI から API に届かない → compose では `ui` → `core-api` は内部 DNS 名。ブラウザからは UI のプロキシ（`/api/core/...`）経由でアクセス

## 環境変数（参照）

| サービス   | 主な変数 |
| ---------- | -------- |
| core-api   | `DATABASE_URL`, `ASPNETCORE_URLS`, `ASPNETCORE_ENVIRONMENT`（compose では `Development`） |
| ui         | `CORE_API_INTERNAL_BASE` |

詳細は `docker-compose.yml` と `AGENTS.md` を参照してください。

## Action Module（plugins）

Core-API は起動時に **modules ルート**（`STATEVIA_MODULES_PATH` または `Statevia:Modules:Path`、未設定時は `{ContentRoot}/modules`）を scan して Action Module を load する。更新・削除の反映は **再起動**または **明示 reload** が必要（add-only watcher は新規追加のみ）。

### 配置（CLI）

```bash
dotnet build cli/statevia-cli.sln
dotnet run --project cli/Statevia.Cli -- module install ./my-module.zip \
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
