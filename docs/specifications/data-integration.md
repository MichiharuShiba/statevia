# データ連携契約

| 項目 | 値 |
| --- | --- |
| 種別 | Specification |
| Version | 1.6 |
| 更新日 | 2026-09-01 |
| Scope | Core-Engine / Service API / UI |
| 関連 | [concepts/durability.md](../concepts/durability.md), [api-http.md](api-http.md) |

---

## Normative 要約

- **MUST**: UI は状態を直接書き換えず、Command を API 経由で発行する。
- **MUST**: HTTP read-model（`GET /v1/executions` / graph）は **DB projection**（`executions` / `execution_graph_snapshots`）を正とする。
- **MUST**: `executions` / グラフスナップショット / cursor / durable wait / `event_store` の更新は、該当ミューテーションの**同一トランザクション**内で整合させる。
- **MUST**: Engine は純粋ロジックとし、I/O は Service API が担当する。
- **SHOULD**: Cancel / Publish 等は契約どおりの tx 分割と Serializable リトライを用いる。

責務の背景は [Concept: 永続化](../concepts/durability.md) を参照。

---

**Version 1.6（2026-09-01）**: `COMMAND_REJECTED` と `X-Correlation-Id` を現行契約から外す。冪等は `StatusCode` / `ResponseBody`。SSE は現行。未実装 Push は Future へ。

**Version 1.5（2026-05-26）**: ExecutionSpace 命名統一（7a〜7d）。HTTP `/v1/executions`、永続 `executions` / `execution_events`、`StartExecutionRequest.input` / `IExecutionEngine.Start(..., input)` に同期。

**Version 1.4（2026-05-20）**: immutable 定義版と実行の `definition_version_id` 固定、publish の truth 先行順序を追記。詳細は [api-http.md](api-http.md) §1.1。

**Version 1.3（2026-05-05）**: 定義 publish `PUT /v1/definitions/{id}`（版 append）と、`DefinitionResponse` の `updatedAt` / `latestVersion` を追記。

**Version 1.2（2026-04-28）**: Definition 向け nodes スキーマ取得 API（`GET /v1/definitions/schema/nodes`）と、Definition 登録時の 422 details 契約を追記。

---

## 0. 全体像（責務の境界）

### Core-Engine（Domain Kernel・ライブラリ）

- 固定イベント・Reducer（優先順位 + normalize）・Command→Event のルール（[events-and-commands.md](execution/events-and-commands.md) 等で仕様化）
- **純粋ロジック**（I/O は API 層が担当）

### Service API（C# / Integration Boundary）

- HTTP API 契約（[api-http.md](api-http.md)）
- 永続化（EF Core）・Read API（`/v1/executions`、`/v1/definitions`）
- **UI Push（SSE）**: `GET /v1/executions/{id}/stream` を実装済み（`Content-Type: text/event-stream`）。グラフ JSON の変化を検知したときに `GraphUpdated` 相当の JSON を `data:` 行で送出する（詳細は §5）。**WebSocket は未実装**。

### UI（Presentation）

- Command 発行（実行開始・キャンセル・イベント）
- Read Model 表示（一覧・詳細・ExecutionGraph）

---

## 1. 連携の基本原則

1. UI は「状態を直接書き換えない」
   - UIは **Command** を送るだけ

2. UI が読むのは **Read Model**（投影）
   - UI は Event を直接読む必要はない（監査画面は例外）

3. Core は “ライブラリ” として Service API に組み込む（現状）
   - 将来マイクロサービス化しても契約（Command / Read / Push）は同一

4. 更新は「Command → 成功レスポンス（`201` / `204` 等）→ Read Model 更新（サーバ側）→ **任意**で SSE 通知」
   - UI は成功レスポンスのあと **GET で Read Model を取得**するか、**SSE を購読**して変化を受けたうえで GET で揃える

---

## 2. データモデル（UIが依存してよい形）

### 2.0 定義版（KnowledgeSpace）

- **truth:** `definition_versions`（`UNIQUE(definition_id, version)`）。publish / 初回作成は **INSERT のみ**。
- **投影:** `definitions.latest_version`（非権威）。
- **実行固定:** `executions.definition_version_id` — Start 時に解決した版を永続化。Engine には当該版の `compiled_json` を供給する。
- **display_id:** UI・運用向け短識別子（namespace 相当の安定識別は `definitionId` + version）。

### 2.1 Execution Read Model（UI向け正規形）

UIが依存してよいレスポンス形を固定する。

```json
{
  "executionId": "ex-1",
  "graphId": "hello",
  "status": "ACTIVE|COMPLETED|FAILED|CANCELED",
  "cancelRequestedAt": "2026-02-28T00:00:00Z|null",
  "canceledAt": "2026-02-28T00:00:00Z|null",
  "failedAt": "2026-02-28T00:00:00Z|null",
  "completedAt": "2026-02-28T00:00:00Z|null",
  "nodes": [
    {
      "nodeId": "task-1",
      "nodeName": "ApproveTask",
      "nodeType": "Task|Wait|Fork|Join|...",
      "status": "IDLE|READY|RUNNING|WAITING|SUCCEEDED|FAILED|CANCELED",
      "attempt": 1,
      "workerId": "w1|null",
      "waitKey": "approval-1|null",
      "allowedEvents": ["approve","reject"]|"null",
      "canceledByExecution": true
    }
  ]
}
```

- **`nodes[*].nodeName`**: 定義上の状態名（StateName と同値）。実行グラフ JSON の `nodeName` を投影する。
- **`nodes[*].nodeType`**: ノード種別（`Task` / `Wait` / `Join` 等）。**`nodeName` とは別**であり、状態名を種別として返してはならない。
- 現行 HTTP の実行ノード識別子は `nodeId`（短名 UUID。定義 YAML の `name` / Graph Def の `nodeName` とは別）。

#### UI側の最低保証

- UIは上記のフィールドのみを前提に描画できる
- ノードやグラフのレイアウト情報（座標など）は **別のGraph定義**として扱う（後述）

---

## 3. Service API（HTTP）: Command 送信と Read 取得

### 3.1 Command API（Write）

- **現行の HTTP ステータス**: `POST /v1/executions` は成功時 **`201 Created`**（ボディに `displayId` / `resourceId` 等）。`POST /v1/definitions` は成功時 **`201 Created`**（`DefinitionResponse`）。`PUT /v1/definitions/{id}` は成功時 **`200 OK`**（`DefinitionResponse`）。`POST /v1/executions/{id}/cancel`・`POST /v1/executions/{id}/events`・`POST /v1/executions/{id}/nodes/{nodeId}/resume` は成功時 **`204 No Content`**。
- **任意ヘッダ**:
  - **`X-Tenant-Id`**: テナントスコープ（`tenant_key`）。Runtime API では Principal と組み合わせて必須。
- **推奨ヘッダ**:
  - `X-Idempotency-Key`（冪等・`event_delivery_dedup` の `client_event_id` 導出。省略時は dedup 保証の対象外）

#### 例: Cancel

`POST /v1/executions/{id}/cancel`（現行）

- 成功時: **`204 No Content`**（レスポンス本文なし）。冪等再送時も同様に **204** で整合（重複適用は抑止される）。

#### Idempotency-Key の保持期間

- Service API は `X-Idempotency-Key` 単位でコマンドを **`command_dedup` テーブルに保存**し、同一キー＋同一エンドポイントの再送時には**重複適用を抑止**する。成功時は初回と同じ HTTP ステータスと、保存した `StatusCode` / `ResponseBody` を返す。
- `command_dedup.expires_at` の **デフォルト値は `created_at + 24h`** とし、この期間を過ぎたレコードはクリーンアップ対象とする。
- **旧 `idempotency_keys` は使用しない（廃止）。冪等は `command_dedup` に一本化している。**

### 3.2 Read API（Query）

- 一覧・詳細はいずれも **`X-Tenant-Id`** でスコープする。他テナントのリソースは返さない（404 相当）。

`GET /v1/executions/{id}`（現行）

- UIは最終状態確認にこれを使う
- コマンド直後の UI 更新は、この GET を**短い間隔でポーリング**してもよいし、**§5 の SSE** を併用してから GET で揃えてもよい

#### 一覧 Query 契約（現行）

- `GET /v1/definitions`
  - `limit` / `offset`（ページング）
  - `name`（部分一致）
  - `sortBy`（`createdAt` / `name`）
  - `sortOrder`（`asc` / `desc`）
- `GET /v1/executions`
  - `limit` / `offset`（ページング）
  - `status`（完全一致）
  - `name`（`display_id` 部分一致。Guid 形式時は `execution_id` 完全一致も許容）
  - `definitionId`（displayId または UUID）
  - `sortBy`（`updatedAt` / `displayId`）
  - `sortOrder`（`asc` / `desc`）

実装上、UI は `limit` / `offset` / `sortBy` / `sortOrder` を URL 同期し、サーバ側で全件対象ソートした結果を受け取る。

#### Definition スキーマ取得（現行）

- **`GET /v1/definitions/schema/nodes`**
- 返却: `schemaVersion`, `nodesVersion`, `schema`
- 用途: UI の補完/Lint の入力源泉（`nodes` 仕様変更時の UI ハードコード追従を抑制）

---

## 3.3 投影・event_store・再送べき等

本節が投影更新、`event_store` に載せる種別、HTTP 再送のべき等の現行契約である。ノード完了ごとのデバウンスや `batchId` 再送は未実装（[sse-and-projection-extensions.md](../future/sse-and-projection-extensions.md)）。

### 投影更新（コマンド同期経路）

- `POST /v1/executions` / `POST /v1/executions/{id}/cancel` / `POST /v1/executions/{id}/events` の各コマンドで、Engine 呼び出し後にスナップショットと実行グラフを取得し、`executions` + `execution_graph_snapshots` を更新する。HTTP コマンドに付随する `event_store` 追記は、下表の種別に限り、可能な範囲で **同一トランザクション**に載せる。
- 途中失敗時はバッチ全体をロールバックし、再送時のべき等は本節の再送べき等に従う。
- **Read Model の正本**: `executions` + `execution_graph_snapshots`。UI・SSE の正本は Read API / 投影済み JSON（§5.1）。
- **`execution_cursors` / `execution_waits`**: 実装済み。cursor は operational projection（GET read-model の正本ではない）。durable wait は初版 EventWait のみ。`Start` / `Cancel` / `Publish` / 投影キューと **同一 tx** で `executions` + `execution_graph_snapshots` と同期する。cursor 行は投影キューと checkpoint 永続化が重なっても PK 単位の原子 upsert で 1 行に収束する。
- 投影キューは HTTP 外のバックグラウンド処理のため、投影更新前に `executions.tenant_id` でテナント境界を設定してから repository を呼ぶ。execution 行が無い場合は投影をスキップする。

SSE（`GET /v1/executions/{id}/stream`）のサーバ挙動（約 2 秒間隔の投影 JSON 比較）は §5.1。

### event_store 対応表（現行）

| event_store `type` | 発火契機（HTTP） | payload（概要） |
| ------------------ | ---------------- | --------------- |
| `WorkflowStarted` | `POST /v1/executions` 成功 | `definitionId`, `tenantId` |
| `WorkflowCancelled` | `POST /v1/executions/{id}/cancel` 成功 | `tenantId` |
| `EventPublished` | `POST /v1/executions/{id}/events` 成功 | `tenantId`, `name` |

ノード完了（Engine 内部）は当面 `event_store` に載せない。

### 再送べき等（現行）

- HTTP コマンドの再送は `command_dedup`（`X-Idempotency-Key` + endpoint）で初回レスポンスを再現する（`StatusCode` / `ResponseBody`）。
- `POST /v1/executions/{id}/cancel` と `POST /v1/executions/{id}/events` では、さらに **`event_delivery_dedup`** テーブルで `(tenant_id, execution_id, client_event_id)` を一意とし、先行 `RECEIVED` → 成功時 `APPLIED`（失敗時 `FAILED`）で配送冪等の正本を DB に持つ。
- `client_event_id` は `X-Idempotency-Key` から解決する（RFC 4122 UUID 形式のキーはその値をそのまま用いる。それ以外は実装既定の決定論的導出）。
- Engine は同一 `client_eventId` の再適用で `AlreadyApplied` を返し得る。API はそれに応じて投影更新は行いつつ `event_store` は **insert-skip** で DB と収束させる。
- `event_delivery_dedup` への `RECEIVED` 先行 INSERT は一時障害時に段階的バックオフで再試行する。
- **API プロセス再起動などで Engine に当該実行が存在しない**とき、コマンド適用は行わず **HTTP 422** とする。投影を壊す更新を防ぐ。

---

## 4. Graph 定義（UI描画に必要な静的データ）

Read Model は「状態」だけなので、UIは別途「構造」を知る必要がある。

### 4.1 Graph Definition（推奨API）

**現行**: `GET /v1/graphs/{graphId}`（`graphId` は定義の **display_id**。`X-Tenant-Id` でスコープ）。UI からはプロキシ経由で `GET /graphs/{graphId}` として呼ぶ構成でもよい（`specifications/api-http.md` に準拠）。

```json
{
  "graphId": "hello",
  "nodes": [
    { "nodeName": "start", "nodeType": "Start", "label": "Start" },
    { "nodeName": "task-1", "nodeType": "Task", "label": "Task A" }
  ],
  "edges": [{ "from": "start", "to": "task-1" }]
}
```

- **Graph Definition** の `edges[*].from` / `to` は **定義キャンバス上の `nodeName`**（状態名と一致しやすい）を指す。一方 **`GET /v1/executions/{id}/graph`** の実行グラフでは **`from` / `to` が実行ノード ID**（`nodes[*].nodeId`）を指す（`docs/specifications/execution/execution-graph.md`）。
- **現行の `GraphDefinitionResponse` には UI 専用のレイアウト塊は含めない**（クライアント側レジストリ等で補う）。上記 `json` の `ui` は参考用の擬似例であり、API 本文には載らない。

#### 重要

- Execution Read Model（状態）と Graph Definition（構造）は分離する
- UIは **構造×状態** を合成して描画する

---

## 5. Realtime Push（SSE）

SSE でサーバから UI へ「再取得のきっかけ」を送ると、ポーリングだけより変化が追いやすい。**WebSocket は現行未実装**。SSE は Principal 必須（未認証は 401）。

### 5.1 SSE エンドポイント

- **`GET /v1/executions/{id}/stream`**（`{id}` は **display_id または resource_id（UUID）**。`X-Tenant-Id` は他 Read API と同様。UI からは `EventSource` 等でプロキシ URL に接続し、テナントは [ui-auth-tenant-config.md](../guides/ui-auth-tenant-config.md) に従う）
- 応答ヘッダ: `Content-Type: text/event-stream`、`Cache-Control: no-cache, no-transform` 等
- **サーバ側挙動**: 約 **2 秒**間隔で投影済みグラフ JSON を読み、内容が前回から変わったときだけ **1 行の `data:`** を書き込む。接続はクライアントが閉じるまで継続。

#### 5.1.1 `data:` 行の JSON 形（`GraphUpdated`）

Service API は Execution Read Model 全体ではなく、グラフスナップショット由来の `patch.nodes` を送る。

```json
{
  "type": "GraphUpdated",
  "executionId": "<execution display_id>",
  "patch": {
    "nodes": {}
  }
}
```

- `executionId`: 実行の **display_id**
- `patch.nodes`: 実行ノード ID をキーとするマップ。`nodeName` / `status` 等を含み得る（[api-http.md](api-http.md)）

本イベントを受けたら **`GET /v1/executions/{id}`** で Read Model を再取得する。SSE は通知であり、正本は Read API である。

`GraphUpdated` 以外の Push 種別は未実装（[sse-and-projection-extensions.md](../future/sse-and-projection-extensions.md)）。

---

## 6. UI の更新戦略（推奨）

### 6.1 GET 再取得（SSE を使わない場合）

- Command 送信（`201` / `204`）
- UI は短い間隔で `GET /v1/executions/{id}` をポーリング（例: 0.5s→1s→2s などバックオフ）
- 状態が終端または一定時間で停止

### 6.2 SSE 併用（現行で利用可能）

- Command 送信（`201` / `204`）
- **`GET /v1/executions/{id}/stream`** を `EventSource` 等で購読し、`GraphUpdated` を受信したタイミングで `GET /v1/executions/{id}` を再取得
- 即時に UI が追従しやすい（§5.1 の約 2 秒間隔に合わせ、過剰な GET を抑えられる）

---

## 7. エラーとUI表現（最小ルール）

- 409 Conflict: 状態競合（例: 論理削除済み定義の復元失敗は `STATE_CONFLICT`。cancel 後 resume など）
- 422: 入力不正、および Engine に実行が無くコマンドを適用できない場合
- 404: execution/node 不在

UI は 409 を「操作できない理由」として表示できる。応答形は [error-codes.md](../reference/error-codes.md)。実装に無い `error.code` は使わない。

Definition 登録（`POST /v1/definitions`）および更新（`PUT /v1/definitions/{id}`）の 422 では、`error.details` に `field` / `message` を含む構造化診断を返し、UI は `name` と `yaml` の表示位置を分離できる。  
同様に、実行開始時の `input`（JSON）・イベント名入力などの入力不正も 422 details で返せる前提とし、UI はフィールド優先で表示する。

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Definition validation failed.",
    "details": [
      { "field": "yaml", "message": "Level 1 validation failed: ..." }
    ]
  }
}
```

---

## 8. シーケンス

### 8.1 GET 再取得

```text
UI -> Service API: POST /v1/executions/{id}/cancel
Service API -> UI: 204 No Content
UI -> Service API: GET /v1/executions/{id}
Service API -> UI: status 等
```

### 8.2 SSE 併用

```text
UI -> Service API: POST /v1/executions/{id}/cancel
Service API -> UI: 204 No Content
UI -> Service API: GET /v1/executions/{id}/stream (SSE、並行で開く)
Service API -> UI: data: {"type":"GraphUpdated", ...}
UI -> Service API: GET /v1/executions/{id}
Service API -> UI: Read Model（確定状態）
```

---

## 9. バージョニング（互換性）

- Read Model は後方互換を守る（フィールド追加は可、削除/意味変更は不可）
- Push payload も同様
- 破壊的変更が必要なら `v2` エンドポイントを追加する
