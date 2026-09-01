# 運用: Docker Compose

| 項目 | 値 |
| --- | --- |
| 種別 | Guide |
| Version | 1.1 |
| 更新日 | 2026-09-01 |
| 関連 | [getting-started.md](getting-started.md), [action-host.md](action-host.md), [environment-variables.md](../reference/environment-variables.md) |

---

リポジトリ直下の `docker-compose.yml` で **PostgreSQL 16**・**Service API（C#）**・**UI（Next.js）**・**Action Host** を起動できます。

**Version 1.1（2026-09-01）**: compose の Action Host 接続を現行として書く。作業番号を外す。

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
- **Service API**: `http://localhost:8080`（`DATABASE_URL` は compose 内で `POSTGRES_*` から組み立て。ホストからの `ef` は `.env` または手元の URL）
- **Action Host**（gRPC）: `http://localhost:5001`。compose は `Statevia__ActionHost__BaseUrl=http://action-host:5001` を Service API / Worker に渡す。OutOfProcess 実行に使う。
- **UI**: `http://localhost:3000`（`SERVICE_API_INTERNAL_BASE=http://service-api:8080`）

## ランタイム分離（任意: Worker / Scheduler）

既定の compose では **Worker / DelayWait Scheduler / Ownership Recovery は Service API プロセス内**で動作します（`Statevia:Runtime:EnableInProcess*` 既定 `true`）。API 内ワーカーで `MaxConcurrency` を 1 より大きくすると、実行負荷が API プロセスに乗ります。

プロセスを分ける場合は、override ファイル `docker-compose.split-runtime.yml` を追加します。これだけで次の両方が同時に効きます。

1. `service-api` の `Statevia__Runtime__EnableInProcess*` を `false` にする
2. `scheduler` / `worker` コンテナを起動する

```bash
docker compose -f docker-compose.yml -f docker-compose.split-runtime.yml up -d
```

| サービス | 役割 |
| -------- | ---- |
| scheduler | 期限切れ DelayWait の排他 claim+Resume enqueue、checkpoint 所有 recovery |
| worker | `execution_work_items` の claim と Start / Resume / Cancel / recovery 実行 |

通常の `docker compose up -d` では `scheduler` / `worker` は定義されず、API 内 HostedService のままです。分離をやめるときは次のように戻します。

```bash
docker compose -f docker-compose.yml -f docker-compose.split-runtime.yml down
docker compose up -d
```

## ヘルス

- Service API: `GET http://localhost:8080/v1/health` → `{ "status": "ok" }`

## API ドキュメント（OpenAPI / Scalar）

`docker compose` の **service-api** は `ASPNETCORE_ENVIRONMENT=Development` のため、次にアクセスできます。

- Scalar UI: `http://localhost:8080/scalar/v1`
- OpenAPI JSON: `http://localhost:8080/swagger/v1/swagger.json`

**本番向けに service-api イメージだけ**を Production で動かす場合は、上記は **既定で無効**です。意図的に有効化する場合は `STATEVIA_ENABLE_API_DOCS=true` を設定してください（API 構造の露出に注意）。

## トラブルシュート

- compose のサービス名を変更したあと `port is already allocated` や `Found orphan containers` が出る → 旧コンテナ（例: `statevia-core-api-1`, `statevia-ui-1`）がポートを占有している。`docker compose up -d --remove-orphans` で orphan を削除するか、`docker compose down --remove-orphans` のあと再起動する
- マイグレーション未適用で API が失敗する → `database update` を実行してから `service-api` を再起動
- UI から API に届かない → compose では `ui-studio` → `service-api` は内部 DNS 名。ブラウザからは UI のプロキシ（`/api/core/...`）経由でアクセス
- Service API が起動直後に落ちる（`OptionsValidationException`）→ `Statevia:ExecutionPolicy:Sandbox:Docker:*` の範囲外値や `Auth:Jwt:*` の不正を確認。キー名と制約はログに出る（値は出ない）。許容範囲は [environment-variables.md](../reference/environment-variables.md)
- 同上で `EventDelivery:Retry:*` も確認 → とくに `MaxDelayMs < BaseDelayMs` は以前黙認されていた誤設定でも **起動失敗**になる
- Container 実行だけ失敗し API は起動する → `Docker:Image` 未設定は起動時検証の対象外（実行時 `SandboxRuntimeUnavailable`）。Image を設定する

## 環境変数（参照）

| サービス   | 主な変数 |
| ---------- | -------- |
| service-api | `DATABASE_URL`（`POSTGRES_*` から組み立て）, `ASPNETCORE_URLS`, `ASPNETCORE_ENVIRONMENT`（compose では `Development`）。分離時は override で `Statevia__Runtime__EnableInProcess*=false`。ブラウザから API へ直叩きする場合のみ `Statevia__Cors__AllowedOrigins__0`（未設定時はクロスオリジン不許可。Studio はプロキシ） |
| postgres    | `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB`（`.env` / `.env.example`） |
| action-host | `ASPNETCORE_URLS`（compose では `http://+:5001`）, `STATEVIA_MODULES_PATH`（`/app/modules`） |
| scheduler / worker | `DATABASE_URL`（`docker-compose.split-runtime.yml`）。worker は `Statevia__ActionHost__BaseUrl` も共有。並列は `Statevia__Runtime__Worker__MaxConcurrency`（既定 1）。Cancel 独立ループは `Statevia__Runtime__Worker__CancelConcurrency`。watchdog は `Statevia__Runtime__Worker__NoProgressTimeout`（既定 `00:10:00`。長い Action は対象外）。API 内ワーカーでも同じキー |
| ui-studio  | `SERVICE_API_INTERNAL_BASE` |

詳細は `docker-compose.yml` / `docker-compose.split-runtime.yml` と [environment-variables.md](../reference/environment-variables.md) を参照してください。

## Action Module（plugins）

Service API は起動時に **modules ルート**（`STATEVIA_MODULES_PATH` または `Statevia:Modules:Path`、未設定時は `{ContentRoot}/modules`）を **テナント別**（`{modulesRoot}/{tenantKey}/{module}/`）に scan して Action Module を load する。更新・削除の反映は **再起動**または **明示 reload** が必要（add-only watcher は新規追加のみ）。

**Action Host**（`docker compose` の `action-host` サービス）は同じ `./modules` ボリュームをマウントし、**別プロセス**で Module を ALC load して gRPC 実行する。Service API は `Statevia:ActionHost:BaseUrl`（compose では `http://action-host:5001`）で接続する。未設定のとき、OutOfProcess が必要な実行は失敗する（安全側）。手順は [action-host.md](action-host.md)。

### 配置（CLI）

```bash
dotnet build service/cli/statevia-cli.sln
dotnet run --project service/cli/Statevia.Service.Cli -- auth login \
  --api-base http://localhost:8080 \
  --tenant default \
  --username ops
dotnet run --project service/cli/Statevia.Service.Cli -- module install ./my-module.zip \
  --modules-path ./modules \
  --tenant default \
  --api-base http://localhost:8080
```

CI ではスコープ `modules.reload` の API キーを `--api-key` または `STATEVIA_API_KEY` に渡します。

- 展開先は **`{modulesRoot}/{tenantKey}/`**（`--tenant` 必須。例: `./modules/default/...`）。
- `--skip-reload` で filesystem 配置のみ。
- reload の `X-Tenant-Id` は install の `--tenant` と一致する。認証は `statevia auth login` の資格情報、またはスコープ `modules.reload` の API キー。
- **セキュリティ**: modules ルートへの書き込みは運用者・デプロイの信頼境界とする（テナントが HTTP から任意配置できない）。

### 運用確認

- `GET /v1/admin/modules`（テナント管理者 JWT または `modules.read`）で **自テナント**の load 状態一覧。
- `POST /internal/modules/reload`（テナント管理者 JWT または `modules.reload`）で **自テナント**の再 scan / load。

詳細は `docs/architecture/repository-layout.md` と `docs/specifications/actions/module-zip-layout.md` を参照。
