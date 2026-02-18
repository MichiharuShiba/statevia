# UI Push API Specification

本ドキュメントは Statevia の ExecutionGraph / 実行状態を
管理 UI に対して Push 配信するための公式 API 仕様を定義する。

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
- API は HTTP + WebSocket/SSE の併用を許可する
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

### GET /api/v1/executions/{executionId}

実行中または完了済み ExecutionGraph の完全スナップショットを取得する。

#### Response

```json
{
  "executionId": "exec-123",
  "status": "Running",
  "graph": { ...ExecutionGraph... },
  "updatedAt": "2026-02-18T01:20:00Z"
}
````

---

## 4. Push 更新（WebSocket or SSE）

### 接続エンドポイント

```txt
GET /api/v1/executions/{executionId}/stream
```

- WebSocket または Server-Sent Events (SSE) を使用
- UI 実装側で選択可能
- 認証ヘッダは REST と同一

---

## 5. Push イベント種別

### 5.1 GraphUpdated

ExecutionGraph の差分更新

```json
{
  "type": "GraphUpdated",
  "executionId": "exec-123",
  "patch": {
    "nodes": [
      {
        "nodeId": "TaskB",
        "status": "Waiting"
      }
    ],
    "edges": []
  },
  "at": "2026-02-18T01:20:03Z"
}
```

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
