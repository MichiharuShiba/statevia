# Core API Contract (HTTP)

Version: 1.0
Project: 実行型ステートマシン
Policy: Cancel wins
Style: RESTful + Idempotent

---

## 0. 設計原則

- APIは **Commandの入口** であり、状態を直接変更しない
- すべての変更は Command → Event → Reducer 経由
- Cancel wins を破らない
- 冪等性を保証する（Idempotency-Key必須推奨）
- 非同期実行前提（202 Acceptedを基本とする）

---

## 1. 共通ヘッダ

### 1.1 必須

- Content-Type: application/json
- X-Idempotency-Key: string（UUID推奨）

### 1.2 任意

- X-Correlation-Id: string
- Authorization: Bearer <token>

---

## 2. 共通レスポンス形式

### 2.1 成功（Accepted）

HTTP 202 Accepted

{
  "executionId": "string",
  "command": "StartNode",
  "accepted": true,
  "correlationId": "string",
  "idempotencyKey": "string"
}

---

### 2.2 拒否（Guard違反）

HTTP 409 Conflict（状態競合）
HTTP 422 Unprocessable Entity（入力不正）
HTTP 404 Not Found

{
  "error": {
    "code": "COMMAND_REJECTED",
    "message": "ResumeNode rejected because execution is cancel-requested",
    "details": {}
  }
}

---

## 3. Execution API

---

### 3.1 Create Execution

POST /executions

Request:
{
  "graphId": "string",
  "input": {}
}

Response:
202 Accepted

内部Command:
CreateExecution

---

### 3.2 Start Execution

POST /executions/{executionId}/start

Command:
StartExecution

Guards:

- execution.status == ACTIVE
- cancelRequestedAt == null

---

### 3.3 Cancel Execution（最重要）

POST /executions/{executionId}/cancel

Command:
CancelExecution

Guards:

- execution が終端でない（冪等許可可）

挙動:

- EXECUTION_CANCEL_REQUESTED を即発行
- その後 EXECUTION_CANCELED を確定（同期/非同期は運用次第）

Cancelは常に優先受理される。

---

### 3.4 Archive Execution

POST /executions/{executionId}/archive

Command:
ArchiveExecution

---

## 4. Node API

---

### 4.1 Start Node

POST /executions/{executionId}/nodes/{nodeId}/start

Request:
{
  "attempt": 1,
  "workerId": "worker-1"
}

Command:
StartNode

---

### 4.2 Report Progress

POST /executions/{executionId}/nodes/{nodeId}/progress

Request:
{
  "progress": 42,
  "message": "processing..."
}

Command:
ReportNodeProgress

---

### 4.3 Put Waiting

POST /executions/{executionId}/nodes/{nodeId}/wait

Request:
{
  "waitKey": "approval-123",
  "prompt": {}
}

Command:
PutNodeWaiting

---

### 4.4 Resume Node

POST /executions/{executionId}/nodes/{nodeId}/resume

Request:
{
  "resumeKey": "approval-123"
}

Command:
ResumeNode

Guards:

- node.status == WAITING
- cancelRequestedAt == null（デフォルト拒否）

---

### 4.5 Succeed Node

POST /executions/{executionId}/nodes/{nodeId}/success

Request:
{
  "output": {}
}

Command:
SucceedNode

---

### 4.6 Fail Node

POST /executions/{executionId}/nodes/{nodeId}/fail

Request:
{
  "error": {
    "code": "ERR_TIMEOUT",
    "message": "timeout"
  }
}

Command:
FailNode

---

## 5. Idempotency（必須仕様）

### 5.1 原則

同一 X-Idempotency-Key + 同一エンドポイント で:

- 同一入力 → 同一結果を返す
- Eventは重複発行しない

### 5.2 保存要素

サーバは以下を保存:

- idempotencyKey
- endpoint
- requestHash
- emittedEventIds
- responsePayload

### 5.3 競合時

- 同Keyで異なる入力 → 409 Conflict

---

## 6. Cancel wins を守る API 層の必須挙動

1. CancelExecution は最優先で受理（終端以外）
2. cancelRequestedAt が存在する場合:
   - 進行系コマンドは 409 で拒否（推奨）
3. reducer 側でも chooseExecStatus により最終保証

---

## 7. 状態取得API（Readモデル）

GET /executions/{executionId}

Response:
{
  "executionId": "...",
  "status": "ACTIVE|COMPLETED|FAILED|CANCELED",
  "cancelRequestedAt": "...",
  "nodes": [
    {
      "nodeId": "...",
      "status": "RUNNING|WAITING|SUCCEEDED|FAILED|CANCELED",
      "canceledByExecution": true|false
    }
  ]
}

※ ReadモデルはEventから投影された結果であり、
Writeモデル（Reducer）とは分離してもよい。

---

## 8. ステータスコード規約

| 状況 | HTTP |
|------|------|
| 受理（非同期） | 202 |
| 冪等再送 | 200 |
| Guard違反 | 409 |
| 入力不正 | 422 |
| 存在しない | 404 |
| 認証失敗 | 401 |
| 権限不足 | 403 |

---

## 9. 非目標

- WebSocket/SSE仕様（別doc）
- UI Push形式（ui-push-update-api.md参照）
- 認可ロール詳細（別仕様化予定）
