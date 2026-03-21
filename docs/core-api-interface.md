# Core API 契約（HTTP）

Version: 1.0
Project: 実行型ステートマシン

Core-API（C#、`api/`）の HTTP 契約。実装に準拠。

- **Base path**: `/v1`
- **Policy**: 終端の優先順位はエンジン内で保証
- **Style**: RESTful

---

## 1. エンドポイント一覧

| メソッド | パス                      | 説明                          |
| -------- | ------------------------- | ----------------------------- |
| GET      | /v1/health                | 死活                          |
| POST     | /v1/definitions           | 定義登録                      |
| GET      | /v1/definitions           | 定義一覧                      |
| GET      | /v1/definitions/{id}      | 定義取得                      |
| POST     | /v1/workflows             | ワークフロー開始              |
| GET      | /v1/workflows             | ワークフロー一覧              |
| GET      | /v1/workflows/{id}        | ワークフロー取得              |
| GET      | /v1/workflows/{id}/graph  | 実行グラフ（JSON）取得        |
| POST     | /v1/workflows/{id}/cancel | キャンセル                    |
| POST     | /v1/workflows/{id}/events | イベント発行（例: Wait 再開） |

---

## 2. Definitions API

### 2.1 定義登録

**POST /v1/definitions**

Request:

```json
{
  "name": "string",
  "yaml": "string"
}
```

Response: 201 Created

```json
{
  "displayId": "string",
  "resourceId": "uuid",
  "name": "string",
  "createdAt": "date-time"
}
```

- `name` / `yaml` 必須。検証・コンパイルして保存。不正時は 400。

### 2.2 定義一覧

**GET /v1/definitions**

- クエリなし: Response 200 OK、`DefinitionResponse[]`（displayId, resourceId, name, createdAt）
- **`?limit=&offset=&name=`**（いずれか指定時）: 200 OK、`PagedResult<DefinitionResponse>`（`items`, `totalCount`, `offset`, `limit`, `hasMore`）。`name` は名前の部分一致。`limit` は 1〜500、`offset` は 0 以上。

### 2.3 定義取得

**GET /v1/definitions/{id}**

- `id`: displayId または UUID
- Response: 200 OK で 1 件。存在しなければ 404。

---

## 3. Workflows API

### 3.1 ワークフロー開始

**POST /v1/workflows**

Request:

```json
{
  "definitionId": "string"
}
```

- `definitionId`: 定義の displayId または UUID
- Response: 201 Created

```json
{
  "displayId": "string",
  "resourceId": "uuid",
  "status": "string",
  "startedAt": "date-time"
}
```

- 定義未存在は 404。検証エラーは 400。

### 3.2 ワークフロー一覧

**GET /v1/workflows**

- クエリなし: Response 200 OK、`WorkflowResponse[]`（displayId, resourceId, status, startedAt, updatedAt, cancelRequested, restartLost）
- **`?limit=&offset=&status=`**（いずれか指定時）: 200 OK、`PagedResult<WorkflowResponse>`。`status` は `workflows.status` 列と**完全一致**。`limit` は 1〜500。

### 3.3 ワークフロー取得

**GET /v1/workflows/{id}**

- Response: 200 OK、**一覧と同一形の `WorkflowResponse`**（UI の WorkflowDTO と整合）。404 は未存在。

### 3.4 実行グラフ取得

**GET /v1/workflows/{id}/graph**

Response: 200 OK、Content-Type: application/json。Engine の ExecutionGraph を JSON で返す。404 は未存在。

### 3.5 キャンセル

**POST /v1/workflows/{id}/cancel**

Response: 204 No Content。エンジンで Cancel を適用し、projection を更新。

### 3.6 イベント発行

**POST /v1/workflows/{id}/events**

Request:

```json
{
  "name": "string"
}
```

- `name`: イベント名（例: Wait の resume 用）。必須。不正時は 400。
- Response: 204 No Content。

---

## 4. 共通

### 4.1 ヘッダ

- **Content-Type**: application/json（Body がある場合）
- **X-Idempotency-Key**: 任意（現行実装では未使用だが推奨）

### 4.2 ステータスコード

| 状況               | HTTP |
| ------------------ | ---- |
| 成功（作成）       | 201  |
| 成功（取得・一覧） | 200  |
| 成功（No Content） | 204  |
| 入力不正           | 400  |
| 存在しない         | 404  |

---

## 5. UI からのアクセス

UI は `/api/core/*` 経由で Core-API にプロキシする。  
例: `/api/core/workflows/xxx` → Core-API の `/v1/workflows/xxx`（route のマッピングは UI 側で実施）。
