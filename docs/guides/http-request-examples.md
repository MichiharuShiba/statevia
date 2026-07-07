# HTTP リクエスト例

| 項目 | 値 |
| --- | --- |
| 種別 | Guide |
| Version | 1.0 |
| 更新日 | 2026-07-07 |
| 関連 | [getting-started.md](getting-started.md), [../specifications/api-http.md](../specifications/api-http.md) |

---

Core-API（`http://localhost:8080`）への代表的な HTTP 呼び出し例。契約の正本は [api-http 仕様](../specifications/api-http.md) です。

## 前提

- `X-Tenant-Id: default`（または運用中の `tenant_key`）
- Runtime API には `Authorization: Bearer <token>` または `X-Api-Key`（将来）
- ミューテーションには `X-Idempotency-Key` を付与（推奨）

トークン取得は [operations-tenant-bootstrap.md](operations-tenant-bootstrap.md) を参照。

## ヘルス

```bash
curl -s http://localhost:8080/v1/health
```

## 定義の初回登録

`POST /v1/definitions` — `Content-Type: application/json` で `name` と `yaml` を送る。応答の `displayId` を実行開始時の `definitionId` に使う。

```bash
DEF_ID=$(curl -s -X POST "http://localhost:8080/v1/definitions" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer <token>" \
  -d "$(jq -n --rawfile yaml docs/samples/ui-customer-order-parallel.yaml \
    '{name:"my-workflow",yaml:$yaml}')" | jq -r .displayId)
```

## 定義の版追加（既存のみ）

`PUT /v1/definitions/{displayId}` — 既存定義に新版を append する。未定義 ID は **404**。

```bash
curl -s -X PUT "http://localhost:8080/v1/definitions/$DEF_ID" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer <token>" \
  -d "$(jq -n --rawfile yaml docs/samples/ui-customer-order-parallel.yaml \
    '{name:"my-workflow",yaml:$yaml}')"
```

## 定義の取得

```bash
curl -s "http://localhost:8080/v1/definitions/$DEF_ID" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer <token>"
```

## 実行の開始

```bash
curl -s -X POST "http://localhost:8080/v1/executions" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer <token>" \
  -H "X-Idempotency-Key: $(uuidgen 2>/dev/null || echo demo-key-1)" \
  -d "{\"definitionId\":\"$DEF_ID\",\"input\":{}}"
```

## 実行状態・グラフ

```bash
EXEC_ID="<execution-id-from-start-response>"

curl -s "http://localhost:8080/v1/executions/${EXEC_ID}" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer <token>"

curl -s "http://localhost:8080/v1/executions/${EXEC_ID}/graph" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer <token>"
```

## キャンセル

```bash
curl -s -X POST "http://localhost:8080/v1/executions/${EXEC_ID}/cancel" \
  -H "Content-Type: application/json" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer <token>" \
  -H "X-Idempotency-Key: $(uuidgen 2>/dev/null || echo demo-key-2)" \
  -d '{}'
```

## SSE（グラフ更新ストリーム）

```bash
curl -N "http://localhost:8080/v1/executions/${EXEC_ID}/stream" \
  -H "X-Tenant-Id: default" \
  -H "Authorization: Bearer <token>"
```

UI からは同一オリジンの `/api/core/executions/{id}/stream` 経由でも可。

## ログイン（開発）

Development では API 起動時に `admin` / `admin` が自動作成される（`tenant_key = default`）。

```bash
curl -s -X POST http://localhost:8080/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"tenantKey":"default","email":"admin","password":"admin"}'
```

## 次に読むもの

- 初回セットアップ: [getting-started.md](getting-started.md)
- OpenAPI / Scalar: [../reference/api-openapi.md](../reference/api-openapi.md)
