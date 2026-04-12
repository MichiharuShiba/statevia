# UI Push API 仕様

Version: 1.1
Project: 実行型ステートマシン

---

本ドキュメントは Statevia の ExecutionGraph / 実行状態を
管理 UI に対して Push 配信するための公式 API 仕様を定義する。

**Version 1.1（2026-04-12）**: Core-API（C#）の実装パスに同期。**SSE** は `GET /v1/workflows/{id}/stream` のみ。**WebSocket は未実装**。REST の実行スナップショットは `GET /v1/workflows/{id}`（および `…/graph`）を正とする。詳細は `docs/statevia-data-integration-contract.md` §5 と `docs/core-api-interface.md` を参照。

本 API は以下の設計方針に基づく:

- RESTful 準拠
- 管理 UI 向け外部公開前提
- Push 型（状態変更が即時 UI に反映される）
- ExecutionGraph を正として UI は参照モデル
- UI は状態を操作せず、操作は別 API 経由とする

---

## 1. 基本方針

- UI は Pull ではなく Push によって最新状態を受信する
- ExecutionGraph はスナップショット + 差分更新の両対応
- API は HTTP + SSE を主とする（**WebSocket は現行未実装**）
- UI 側は常にサーバー状態を正とする

---

## 2. 認証・テナント

### 2.1 ヘッダ

```txt

Authorization: Bearer <token>
X-Tenant-Id: <tenant-id>

````

- テナント ID は将来的な SaaS 展開を考慮して必須
- UI は複数テナントを扱える設計を前提とする

---

## 3. 初期スナップショット取得（REST）

### GET /v1/workflows/{id}

実行（ワークフロー）の Read Model を取得する。`{id}` は **display_id** または **resource_id（UUID）**。

- 完全な ExecutionGraph JSON が必要な場合は **`GET /v1/workflows/{id}/graph`** を併用する。
- UI からは `/api/core/workflows/{id}` 等にプロキシしてもよい（`docs/core-api-interface.md` §5）。

#### Response（例: 一覧と同一の `WorkflowResponse` 形）

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

### 接続エンドポイント（Core-API）

```txt
GET /v1/workflows/{id}/stream
```

- **Server-Sent Events (SSE)** のみ（`Content-Type: text/event-stream`）。**WebSocket は未実装**。
- サーバは投影グラフを約 2 秒周期で比較し、変化時に `data:` 行を 1 件書き込む（長接続）。
- **テナント**: `X-Tenant-Id`（`EventSource` でヘッダを付けられない場合は `docs/ui-api-auth-tenant-config.md` の `?tenantId=` 経由）。
- ペイロード形式は §5.1 および `docs/statevia-data-integration-contract.md` §5.1.1 を参照。

---

## 5. Push イベント種別

**現行 Core-API の SSE が送出するのは §5.1 `GraphUpdated` のみ**（§5.2 以降は将来拡張・別チャネル用の論理例として残す）。

### 5.1 GraphUpdated

ExecutionGraph の差分更新（現行 Core-API が SSE で送出する形に準拠）。

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
- `patch.nodes`: 実装では **ノード ID → パッチオブジェクト** のマップになることが多い（配列形式の例は論理説明用）。
- 現行実装の JSON に **`at` は含まれない**（時刻が必要ならクライアント側で受信時刻を付与するか、`GET /v1/workflows/{id}` を再取得する）。

---

### 5.2 ExecutionStatusChanged

Execution 全体の状態変更

```json
{
  "type": "ExecutionStatusChanged",
  "executionId": "exec-123",
  "from": "Running",
  "to": "Cancelled",
  "reason": "UserRequest",
  "at": "2026-02-18T01:20:05Z"
}
```

---

### 5.3 NodeCancelled

ノード単位のキャンセル通知

```json
{
  "type": "NodeCancelled",
  "executionId": "exec-123",
  "nodeId": "TaskC",
  "cancel": {
    "reason": "ParentCancelled",
    "cause": {
      "message": "Fork scope cancelled",
      "at": "2026-02-18T01:20:05Z"
    }
  }
}
```

---

### 5.4 NodeFailed

```json
{
  "type": "NodeFailed",
  "executionId": "exec-123",
  "nodeId": "TaskA",
  "error": {
    "message": "Unhandled exception",
    "at": "2026-02-18T01:20:02Z"
  }
}
```

---

## 6. UI 操作 API（操作は Pull / Command）

UI は状態を直接変更しない。
操作は Command API として分離する。

### 6.1 Resume

```txt
POST /api/v1/executions/{executionId}/resume
```

```json
{
  "event": "OrderConfirmed",
  "payload": {
    "orderId": "12345"
  }
}
```

---

### 6.2 Cancel

```txt
POST /api/v1/executions/{executionId}/cancel
```

```json
{
  "reason": "UserRequest",
  "cause": {
    "message": "Stopped by admin"
  }
}
```

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

## 9. セキュリティ設計指針

- UI からの操作はすべて認可チェック対象
- テナント ID はすべての API で検証される
- ExecutionGraph の Payload は redactionPolicy に従いマスクされる

---

## 10. 将来的な拡張

- GraphUpdated の JSON Patch 対応
- 複数 Execution のストリーム購読
- UI 側フィルタリング（Failed のみ購読など）
