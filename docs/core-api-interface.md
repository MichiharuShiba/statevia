# Core API 契約（HTTP）

Version: 1.1
Project: 実行型ステートマシン

Core-API（C#、`api/`）の HTTP 契約。実装に準拠。

**Version 1.1（2026-04-12）**: Graph Definition、`GET …/state` / `GET …/events` / `GET …/stream`（SSE）、`POST …/nodes/.../resume` を一覧・本文に追加。

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
| GET      | /v1/graphs/{graphId}      | Graph Definition（nodes/edges） |
| POST     | /v1/workflows             | ワークフロー開始              |
| GET      | /v1/workflows             | ワークフロー一覧              |
| GET      | /v1/workflows/{id}        | ワークフロー取得              |
| GET      | /v1/workflows/{id}/graph  | 実行グラフ（JSON）取得        |
| GET      | /v1/workflows/{id}/state  | 状態ビュー（`atSeq` クエリ必須） |
| GET      | /v1/workflows/{id}/events  | event_store タイムライン（`afterSeq`, `limit`） |
| GET      | /v1/workflows/{id}/stream  | SSE（グラフ変化を `GraphUpdated` で送出） |
| POST     | /v1/workflows/{id}/cancel | キャンセル                    |
| POST     | /v1/workflows/{id}/events | イベント発行（例: Wait 再開） |
| POST     | /v1/workflows/{id}/nodes/{nodeId}/resume | ノード再開（body: `resumeKey`） |

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

### 2.4 Graph Definition（構造）

**GET /v1/graphs/{graphId}**

- `graphId`: 定義の **displayId** または UUID（実装コメント上は display_id 解決）。
- **X-Tenant-Id**: 任意。省略時 `"default"`。
- Response: 200 OK、`GraphDefinitionResponse`（`graphId`, `nodes[]`, `edges[]` 等。詳細は実装の `GraphDefinitionResponse`）。
- 404: 定義が存在しない、または当該テナントに無い。

---

## 3. Workflows API

### 3.1 ワークフロー開始

**POST /v1/workflows**

Request:

```json
{
  "definitionId": "string",
  "input": {
    "foo": "bar"
  }
}
```

- `definitionId`: 定義の displayId または UUID
- `input`: 任意の JSON 値（省略可）。初期状態へ `workflowInput` として渡される。
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

- JSON キー命名は **camelCase**。
- 条件遷移を評価したノードは `conditionRouting` を含む。
  - 主要キー: `fact`, `resolution`, `matchedCaseIndex`, `caseEvaluations`, `evaluationErrors`
  - `resolution` は `linear` / `matched_case` / `default_fallback` / `no_transition`

### 3.5 状態ビュー（UI）

**GET /v1/workflows/{id}/state?atSeq={seq}**

- **atSeq**: 必須（long）。`event_store` のシーケンスに基づく状態ビュー（`WorkflowViewDto`）。リプレイ用途。
- Response: 200 OK。404 は未存在。
- `WorkflowViewDto.nodes[*].conditionRouting` は、実行グラフの `conditionRouting` を API が透過的に返したもの（UI 側で再評価しない）。

### 3.6 イベントタイムライン（Read）

**GET /v1/workflows/{id}/events**

- クエリ: **`afterSeq`**（既定 0）、**`limit`**（既定 500、上限は実装に従う）。
- Response: 200 OK、`ExecutionEventsResponseDto`（タイムライン行の列挙）。404 は未存在。

### 3.7 SSE（グラフ変化の Push）

**GET /v1/workflows/{id}/stream**

- Response: **`200`**、`Content-Type: text/event-stream`。本文は SSE の **`data:`** 行に JSON（`type: GraphUpdated` 等）。接続維持型（サーバは約 2 秒周期で投影グラフを比較し、変化時のみ `data:` を書き込む）。
- 認証は現行未実装。テナントは **`X-Tenant-Id`**（UI から `EventSource` でヘッダが付けられない場合は `docs/ui-api-auth-tenant-config.md` のクエリ経由を参照）。
- 詳細ペイロードは `docs/statevia-data-integration-contract.md` §5.1 を正とする。
- 404: ワークフロー未存在。

### 3.8 キャンセル

**POST /v1/workflows/{id}/cancel**

- **X-Idempotency-Key**: 任意だが推奨。同一キー＋同一リクエストの再送は `command_dedup` により初回と同じ結果（通常 204）を返す。キーは `event_delivery_dedup` の `client_event_id` 導出にも使われる（詳細は `docs/statevia-data-integration-contract.md` の STV-415）。
- Response: 204 No Content。エンジンで Cancel を適用し、projection を更新。
- Engine に当該ワークフローが無い（例: API 再起動直後）場合は **422**（`ArgumentException`。データ連携契約のセクション7）。

### 3.9 イベント発行（Write）

**POST /v1/workflows/{id}/events**

Request:

```json
{
  "name": "string"
}
```

- `name`: イベント名（例: Wait の resume 用）。必須。不正時は 400。
- **X-Idempotency-Key**: 任意だが推奨。再送・重複排除の扱いはキャンセルと同様（`command_dedup` + `event_delivery_dedup` / `client_event_id`）。
- Response: 204 No Content。
- Engine に当該ワークフローが無い場合は **422**（キャンセルと同様）。

### 3.10 ノード再開

**POST /v1/workflows/{id}/nodes/{nodeId}/resume**

Request（JSON、省略可）:

```json
{
  "resumeKey": "string"
}
```

- **X-Idempotency-Key**: 任意だが推奨（キャンセル・イベント発行と同様の冪等・配送抑止）。
- Response: 204 No Content。422 / 404 の扱いは実装および `docs/statevia-data-integration-contract.md` §7 に従う。

---

## 4. 共通

### 4.1 ヘッダ

- **Content-Type**: application/json（Body がある場合）
- **X-Idempotency-Key**: 任意。`POST /v1/workflows` では `definitionId + input` を含むリクエストハッシュで冪等キーを分離する（同一キーでも input が異なれば別リクエスト扱い）。

### 4.2 JSON 命名ポリシー（実装準拠）

- Core-API が返す JSON は原則 **camelCase** を採用する。
- `GET /v1/workflows/{id}/graph`（ExecutionGraph JSON）と、定義コンパイル由来のデバッグ JSON（`compiledJson`）は camelCase で統一済み。

### 4.3 ステータスコード

| 状況               | HTTP |
| ------------------ | ---- |
| 成功（作成）       | 201  |
| 成功（取得・一覧） | 200  |
| 成功（No Content） | 204  |
| 入力不正           | 400  |
| 存在しない         | 404  |
| 冪等キー再利用（別リクエスト本文） | 409 。`error.code` は `IDEMPOTENCY_KEY_CONFLICT`（`POST /v1/workflows` のみ） |
| コマンド適用不可（例: Engine にワークフローが無い） | 422 。`ArgumentException` がマッピングされる（データ連携契約のセクション7） |

---

## 5. UI からのアクセス

UI は `/api/core/*` 経由で Core-API にプロキシする。  
例: `/api/core/workflows/xxx` → Core-API の `/v1/workflows/xxx`（route のマッピングは UI 側で実施）。
