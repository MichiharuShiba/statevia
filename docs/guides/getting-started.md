# Quick Start — 初回セットアップ

| 項目 | 値 |
| --- | --- |
| 種別 | Guide |
| Version | 1.0 |
| 更新日 | 2026-07-07 |
| 関連 | [operations-docker.md](operations-docker.md), [operations-tenant-bootstrap.md](operations-tenant-bootstrap.md), [api-http.md](../specifications/api-http.md) |

---

Docker Compose で PostgreSQL・Core-API・UI を起動し、API で定義を publish して実行するまでの最短手順。

## 前提

- Docker / Docker Compose が使えること
- .NET 8 SDK（マイグレーションをホストから実行する場合）
- リポジトリルートに `.env`（初回は `.env.example` をコピー）

## 1. データベースとマイグレーション

```bash
cp .env.example .env   # 初回のみ
docker compose up -d postgres
```

ホストから EF Core マイグレーション（bash の例。`.env` の認証情報を使い、ホスト名だけ `localhost` にする）:

```bash
set -a && source .env && set +a
export DATABASE_URL="postgres://${POSTGRES_USER}:${POSTGRES_PASSWORD}@localhost:${POSTGRES_PORT}/${POSTGRES_DB}"
cd service/api
dotnet ef database update --project Statevia.Service.Api
```

PowerShell の例:

```powershell
$env:DATABASE_URL = "postgres://statevia:statevia@localhost:5432/statevia"
cd service/api
dotnet ef database update --project Statevia.Service.Api
```

マイグレーション適用時に `tenant_key = default` のテナントがシードされる。詳細は [operations-tenant-bootstrap.md](operations-tenant-bootstrap.md)。

## 2. Core-API と UI の起動

```bash
docker compose up -d
```

§1 で postgres のみ起動してマイグレーションしたあと、上記で残りのサービスを起動する。すでに全サービスを起動済みで API が DB エラーになっている場合は、マイグレーション後に `docker compose restart service-api`。

| サービス | URL |
| --- | --- |
| Core-API | `http://localhost:8080` |
| UI | `http://localhost:3000` |
| Scalar（API 閲覧） | `http://localhost:8080/scalar/v1` |

ヘルス確認: `GET http://localhost:8080/v1/health` → `{ "status": "ok" }`

トラブルシュートは [operations-docker.md](operations-docker.md) を参照。

## 3. 認証（Runtime API）

`/v1/definitions` と `/v1/executions` は **Principal 必須**（JWT または `X-Api-Key`）+ `X-Tenant-Id`（`tenant_key`、既定 `default`）。

**Development**（`docker compose` の既定）では API 起動時に次の管理者が自動作成される（`skip-if-exists`、マイグレーション適用後）:

| 項目 | 値 |
| --- | --- |
| テナント | `default` |
| ユーザー（email） | `admin` |
| パスワード | `admin` |

```bash
curl -s -X POST http://localhost:8080/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"tenantKey":"default","email":"admin","password":"admin"}'
```

応答の `accessToken` を `Authorization: Bearer <token>` に設定する。本番・追加テナントの手動作成は [operations-tenant-bootstrap.md](operations-tenant-bootstrap.md) を参照。

## 4. 定義の登録（初回）

初回は **`POST /v1/definitions`**（`201 Created`）。`PUT /v1/definitions/{id}` は **既存定義への版追加** のみで、未定義の ID では **404** になる。

本文は **`application/json`**（`name` + `yaml`）。詳細は [api-http.md](../specifications/api-http.md)。

```bash
TOKEN=$(curl -s -X POST http://localhost:8080/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"tenantKey":"default","email":"admin","password":"admin"}' | jq -r .accessToken)

DEF_ID=$(curl -s -X POST "http://localhost:8080/v1/definitions" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" \
  -d "$(jq -n --rawfile yaml docs/samples/ui-customer-order-parallel.yaml \
    '{name:"my-workflow",yaml:$yaml}')" | jq -r .displayId)
```

YAML を改訂して再 publish する場合は `PUT /v1/definitions/$DEF_ID` を使う（版が 1 つ増える）。

定義 YAML の書き方は [definition.md](../specifications/definition.md)。

## 5. 実行の開始

```bash
curl -s -X POST "http://localhost:8080/v1/executions" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Idempotency-Key: $(uuidgen)" \
  -d "{\"definitionId\":\"$DEF_ID\",\"input\":{}}"
```

実行状態・グラフは `GET /v1/executions/{id}` / `GET /v1/executions/{id}/graph`。契約は [api-http.md](../specifications/api-http.md)。

## 6. UI で確認

ブラウザで `http://localhost:3000` を開き、同一オリジンのプロキシ経由で Core-API に接続する。画面操作は [ui-user-guide.md](ui-user-guide.md) を参照。

## 次に読むもの

- 定義の詳細: [definition.md](../specifications/definition.md)
- 思想・全体像: [concepts/README.md](../concepts/README.md)
- 索引全体: [docs/README.md](../README.md)

## ローカル開発（Compose を使わない場合）

- PostgreSQL のみ Docker、API/UI はホスト起動: [AGENTS.md](../../AGENTS.md) の Running services を参照
- Engine ライブラリのみ: [engine-standalone-guide.md](engine-standalone-guide.md)
