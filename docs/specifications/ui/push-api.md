# UI Push API 仕様

| 項目 | 値 |
| --- | --- |
| 種別 | Specification |
| Version | 1.3 |
| 更新日 | 2026-09-01 |
| 関連 | [data-integration.md](../data-integration.md), [api-http.md](../api-http.md), [../guides/ui-auth-tenant-config.md](../../guides/ui-auth-tenant-config.md) |

---

## Normative 要約

- **MUST**: リアルタイム配信は **SSE** のみ（`GET /v1/executions/{id}/stream`）。WebSocket は未実装。
- **MUST**: SSE は Principal 必須（JWT または `X-Api-Key`）+ `X-Tenant-Id`。未認証は **401**。
- **MUST**: UI は状態を直接書き換えず、操作は REST Command API 経由とする。
- **MUST**: Push ペイロードは ExecutionGraph / 実行状態の read-model に整合すること。
- **SHOULD**: UI は同一オリジン `/api/core/*` プロキシ経由で Service API に接続する。

---

**Version 1.3（2026-09-01）**: SSE は Principal 必須。テナントは現行必須。未送出 Push 種別を現行本文から外す。

## 1. 基本方針

- UI は Pull ではなく Push によって最新状態を受信する
- ExecutionGraph はスナップショット + 差分更新の両対応
- API は HTTP + SSE を主とする（**WebSocket は現行未実装**）
- UI 側は常にサーバー状態を正とする

---

## 2. 認証・テナント

```txt
Authorization: Bearer <token>
X-Tenant-Id: <tenant_key>
```

- Runtime API と同じく Principal と `X-Tenant-Id` が必須である。
- ブラウザの `EventSource` はヘッダを送れない。Studio は同一オリジンのプロキシが Cookie の JWT を付け、テナントは `?tenantId=` から `X-Tenant-Id` に変換する（[ui-auth-tenant-config.md](../../guides/ui-auth-tenant-config.md)）。

---

## 3. 初期スナップショット取得（REST）

### GET /v1/executions/{id}

実行の Read Model を取得する。`{id}` は **display_id** または **resource_id（UUID）**。

- 完全な ExecutionGraph JSON が必要な場合は **`GET /v1/executions/{id}/graph`** を併用する。
- UI からは `/api/core/executions/{id}` 等にプロキシしてもよい（[api-http.md](../api-http.md) §5）。

#### Response（例: 一覧と同一の `ExecutionResponse` 形）

```json
{
  "displayId": "Ab3Cd9Fg2K",
  "resourceId": "0198b3e4-0000-7000-8000-000000000001",
  "status": "Running",
  "startedAt": "2026-02-18T01:20:00Z",
  "updatedAt": "2026-02-18T01:20:05Z",
  "cancelRequested": false,
  "restartLost": false
}
```

---

## 4. Push 更新（SSE）

```txt
GET /v1/executions/{id}/stream
```

- **Server-Sent Events (SSE)** のみ（`Content-Type: text/event-stream`）。**WebSocket は未実装**。
- Principal 必須。未認証は **401**。
- サーバは投影グラフを約 2 秒周期で比較し、変化時に `data:` 行を 1 件書き込む（長接続）。
- **テナント**: `X-Tenant-Id`（`EventSource` では [ui-auth-tenant-config.md](../../guides/ui-auth-tenant-config.md) の `?tenantId=` 経由）。
- ペイロード形式は §5 および [data-integration.md](../data-integration.md) §5.1.1 を参照。

---

## 5. Push イベント種別

現行 Service API の SSE が送出するのは **`GraphUpdated` のみ**。他種別は未実装（[sse-and-projection-extensions.md](../../future/sse-and-projection-extensions.md)）。

### 5.1 GraphUpdated

```json
{
  "type": "GraphUpdated",
  "executionId": "Ab3Cd9Fg2K",
  "patch": {
    "nodes": {}
  }
}
```

- `executionId`: ワークフローの **display_id**。
- `patch.nodes`: 実装では **ノード ID → パッチオブジェクト** のマップになることが多い。
- 現行実装の JSON に **`at` は含まれない**（時刻が必要ならクライアント側で受信時刻を付与するか、`GET /v1/executions/{id}` を再取得する）。

---

## 6. UI 操作（Command）

UI は状態を直接変更しない。操作は [api-http.md](../api-http.md) の Command API に従う。

- キャンセル: `POST /v1/executions/{id}/cancel`
- Wait 再開: `POST /v1/executions/{id}/nodes/{nodeId}/resume`
- 集合配送: `POST /v1/events`

---

## 7. 競合ルール

- Resume と Cancel が競合した場合は Cancel が優先される
- UI は競合状態を制御しない
- UI は結果のみを Push イベントで受信する

---

## 8. 再接続時の整合性保証

- Push 接続が切断された場合、UI は必ず REST スナップショットを再取得する
- Push は差分であるため、UI は再接続時に完全同期を行うこと

---

## 9. セキュリティ

- UI からの操作はすべて認可チェック対象
- テナント ID はすべての API で検証される
- ExecutionGraph の Payload はログマスキング方針に従いマスクされる（[io-log-masking.md](../platform/io-log-masking.md)）
