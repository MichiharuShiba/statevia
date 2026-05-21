# Core API 契約（HTTP）

Version: 1.6
Project: 実行型ステートマシン

Core-API（C#、`api/`）の HTTP 契約。実装に準拠。

**Version 1.6（2026-05-20）**: immutable 定義版（`definitions` / `definition_versions`）と実行時の `definitionVersionId` 固定を追記。`PUT /v1/definitions/{id}` は **版追加（publish）** として記述。Start リクエストに `definitionVersion` / `definitionVersionId` を追加。

**Version 1.5（2026-05-11）**: 実行グラフ JSON の **`edges[*].from` / `edges[*].to`**（実行ノード ID）と、`GET …/state` の **`WorkflowViewDto.nodes[*]`**（`executionNodeId` と `stateName` の分離、運用メタ・入出力）を契約本文に明記。

- **Base path**: `/v1`
- **Policy**: 終端の優先順位はエンジン内で保証
- **Style**: RESTful

## OpenAPI / Scalar（機械可読な契約）

| 項目 | 内容 |
| --- | --- |
| スキーマ・操作の正本 | Swashbuckle 生成の `/swagger/v1/swagger.json`、およびリポジトリ内 [`api/openapi/core-api-v1.openapi.json`](../api/openapi/core-api-v1.openapi.json) |
| 閲覧 UI（Development） | Scalar — 例: `http://localhost:8080/scalar/v1`（`ASPNETCORE_URLS` に依存） |
| 本番 | OpenAPI / Scalar は **既定オフ**。Staging または `STATEVIA_ENABLE_API_DOCS=true` で有効化 |
| export | リポジトリルートから `.\scripts\export-core-api-openapi.ps1` |
| 本書の役割 | SSE・冪等・IO-14・Read-model 注意など、OpenAPI に載せにくい運用叙述を維持 |

エンドポイント詳細は段階的に OpenAPI へ寄せる。以下 §2 以降の JSON 例は移行期の参考であり、差異がある場合は OpenAPI を優先する。

---

## 1.1 定義データモデル（truth / projection）

| 対象 | 役割 |
| --- | --- |
| `definition_versions` + `UNIQUE(definition_id, version)` | 定義版の **truth**（immutable） |
| `definitions.latest_version` | **投影**（非権威。省略時 Start の版解決にのみ使用） |
| `workflows.definition_version_id` | 実行開始時に固定した版への FK（execution correctness） |

**禁止事項:**

- 既存 `definition_versions` 行の `source_yaml` / `compiled_json` を **上書きしない**（更新は新版 INSERT のみ）。
- publish トランザクションで **`latest_version` のみ先行コミット** しない（同一 ReadCommitted tx 内で version INSERT → latest 更新）。
- Start 時に **mutable な定義行**や **latest だけ**から `compiled_json` を取得しない（必ず解決した **version 行**の `compiled_json` を使用）。

**移行期（フェーズ 1a）:** `definitions.project_id` は NULL 可。テナント境界は `definitions.tenant_id` で担保（`projects` / `project_accesses` 導入後に認可 truth を移行）。

**publish トランザクション:** `ICoreTransactionExecutor.ExecuteReadCommittedAsync` の 1 コールバック内で `definition_versions` INSERT → `definitions.latest_version` 更新。

---

## 1. エンドポイント一覧

| メソッド | パス                      | 説明                          |
| -------- | ------------------------- | ----------------------------- |
| GET      | /v1/health                | 死活                          |
| POST     | /v1/definitions           | 定義登録                      |
| PUT      | /v1/definitions/{id}      | 定義更新（displayId または UUID） |
| GET      | /v1/definitions           | 定義一覧                      |
| GET      | /v1/definitions/{id}      | 定義取得                      |
| GET      | /v1/definitions/schema/nodes | nodes 入力スキーマ取得     |
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

**POST /v1/definitions** — リクエスト / 応答スキーマは OpenAPI の `CreateDefinitionRequest` / `DefinitionResponse`（`201 Created`）を参照。

- `name` / `yaml` 必須。検証・コンパイルして **初版（version=1）** を `definition_versions` に保存し、`definitions` 行を作成。新規行では `updatedAt` は `createdAt` と同一。`latestVersion` は `1`。不正時は 422（`error.details` に field/message を含む）。

### 2.1.1 定義 publish（版追加）

**PUT /v1/definitions/{id}**

- `id`: displayId または UUID
- Request: `POST` と同形（`name`, `yaml` 必須）
- 既存版は変更せず **新版を append** する（mutable 上書きではない）。
- Response: **200 OK**、`DefinitionResponse`（`latestVersion` が増加。`GET` と同様に **最新版**の `yaml` を含む）。
- 存在しない／他テナント: **404**。検証・コンパイル失敗: **422**（`POST` と同様の `error.details`）。
- 並行 publish で `UNIQUE(definition_id, version)` 競合: **422**（成功した版のみ truth）。

### 2.2 定義一覧

**GET /v1/definitions**

- **`?limit=&offset=&name=&sortBy=&sortOrder=`**（`limit` 必須）: 200 OK、`PagedResult<DefinitionResponse>`（`items`, `totalCount`, `offset`, `limit`, `hasMore`）。
  - `name`: 名前の部分一致
  - `sortBy`: `createdAt` / `name`（未指定時は `createdAt`）
  - `sortOrder`: `asc` / `desc`（未指定時は `desc`）
  - `limit`: 1〜500（必須）、`offset`: 0 以上（省略時 0）
- `limit` 未指定・不正: **422**

**移行:** 一覧取得は `?limit=N&offset=M` を必須とする。全件が必要な場合は `limit=500` と `hasMore` を用いてページを繰り返す。

### 2.3 定義取得

**GET /v1/definitions/{id}**

- `id`: displayId または UUID
- Response: 200 OK で 1 件（`displayId`, `resourceId`, `name`, `latestVersion`, `createdAt`, `updatedAt`, **`yaml`**（**最新版**の保存済みソース））。存在しなければ 404。

### 2.4 Graph Definition（構造）

**GET /v1/graphs/{graphId}**

- `graphId`: 定義の **displayId** または UUID（実装コメント上は display_id 解決）。
- **X-Tenant-Id**: 任意。省略時 `"default"`。
- Response: 200 OK、`GraphDefinitionResponse`（`graphId`, `nodes[]`, `edges[]` 等。詳細は実装の `GraphDefinitionResponse`）。
- 404: 定義が存在しない、または当該テナントに無い。

### 2.5 nodes スキーマ取得

**GET /v1/definitions/schema/nodes**

- UI の補完/Lint 源泉として利用する入力スキーマを返す。
- Response: 200 OK

```json
{
  "schemaVersion": "1.0.0",
  "nodesVersion": 1,
  "schema": {}
}
```

---

## 3. Workflows API

### 3.1 ワークフロー開始

**POST /v1/workflows**

Request:

```json
{
  "definitionId": "string",
  "definitionVersion": 1,
  "definitionVersionId": "uuid",
  "input": {
    "foo": "bar"
  }
}
```

- `definitionId`: 定義の displayId または UUID（必須）
- `definitionVersion`: 版番号（任意）。省略時は `definitions.latest_version` を使用
- `definitionVersionId`: 版 UUID（任意）。**指定時は `definitionVersion` より優先**。他テナント・他定義の版は **404**
- **再現性:** 本番運用では `definitionVersion` または `definitionVersionId` の **明示を推奨**（latest 省略は開発・探索用途）
- `input`: 任意の JSON 値（省略可）。初期状態へ `workflowInput` として渡される
- Engine 投入は解決した **version 行の `compiled_json`**（同一版の `source_yaml` で executor を復元）
- 永続化: `workflows.definition_version_id` に開始版を必ず保存（Start は ReadCommitted 1 tx: `workflows` + snapshot + `event_store` + dedup）
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

- **`?limit=&offset=&status=&name=&definitionId=&sortBy=&sortOrder=`**（`limit` 必須）: 200 OK、`PagedResult<WorkflowResponse>`。
  - `status`: `workflows.status` 列と**完全一致**
  - `name`: `display_id` の部分一致（`Guid` 形式入力時は `workflow_id` 完全一致も許容）
  - `definitionId`: Definition の displayId または UUID
  - `sortBy`: `updatedAt` / `displayId`（未指定時は `updatedAt`）
  - `sortOrder`: `asc` / `desc`（未指定時は `desc`）
  - `limit`: 1〜500（必須）、`offset`: 0 以上（省略時 0）
- `limit` 未指定・不正: **422**

### 3.3 ワークフロー取得

**GET /v1/workflows/{id}**

- Response: 200 OK、**一覧と同一形の `WorkflowResponse`**（UI の WorkflowDTO と整合）。404 は未存在。

### 3.4 実行グラフ取得

**GET /v1/workflows/{id}/graph**

Response: 200 OK、Content-Type: application/json。`execution_graph_snapshots` に保存された **ExecutionGraph と同形の JSON** を返す。404 は未存在。

- JSON キー命名は **camelCase**。
- トップレベルは **`nodes`**, **`edges`**（ExecutionGraph のシリアライズ形）。**HTTP** では `execution_graph_snapshots` の行が無い場合は **404**（`WorkflowService.GetGraphJsonAsync`）。エンジン API `ExportExecutionGraph` がメモリにインスタンスを持たないときは **`{}`** 文字列を返し得るが、それは in-process 観測用であり、Read API の正本ではない（`AGENTS.md` Read-model authority）。
- **`nodes[*].nodeId`**: ランタイム実行ノード ID（短いランダム ID）。**定義**の `GET /v1/graphs/{graphId}` における **`nodes[*].nodeId`（状態名ベースのキャンバス ID）とは別**。
- **`nodes[*].stateName`**: 定義上の状態名。UI はマージ時に `stateName` および実行エッジで対応付ける（`docs/core-engine-execution-graph-spec.md` §7）。
- **`edges[*].from` / `edges[*].to`**: いずれも **`nodes[*].nodeId`** を指す。旧キー `fromNodeId` / `toNodeId` は用いない。
- **`edges[*].type`**: `EdgeType` の数値（`Next` 0 など）。`Join`（2）では合流元から Join 合成ノードへ **複数辺** が立ち得る。
- 条件遷移を評価したノードは `conditionRouting` を含む。
  - 主要キー: `fact`, `resolution`, `matchedCaseIndex`, `caseEvaluations`, `evaluationErrors`
  - `resolution` は `linear` / `matched_case` / `default_fallback` / `no_transition`
- ノードの **`input` / `output` / `attempt` / `workerId` / `waitKey` / `canceledByExecution` / `nodeType`** などの詳細は `docs/core-engine-execution-graph-spec.md` §4 を正とする。

**IO-14**: グラフ JSON に含まれる `input` / `output` は機微情報になり得る。一覧 `GET /v1/workflows` 等では既定で返さない方針は `AGENTS.md` の Input/Output exposure policy に従う。

### 3.5 状態ビュー（UI）

**GET /v1/workflows/{id}/state?atSeq={seq}**

- **atSeq**: 必須（long）。`event_store` のシーケンスに基づく状態ビュー（`WorkflowViewDto`）。リプレイ用途。
- Response: 200 OK。404 は未存在。
- 現行実装は **スナップショット近似**であり、`atSeq` 時点の厳密な過去状態の完全再構成を保証するものではない（運用上の注意は UI 文言・将来の強化チケットに委ねる）。
- **`WorkflowViewDto`** は UI の `WorkflowView` に近い camelCase。`displayId`, `resourceId`, `graphId`, `status`, `startedAt`, `updatedAt`, `cancelRequested`, `restartLost`, **`nodes`**。
- **`WorkflowViewDto.nodes[*]`**（`WorkflowViewNodeDto`）の主なフィールド:
  - **`executionNodeId`**: 実行グラフの **`nodeId`** と一致させる識別子（試行単位の実行ノード）。
  - **`stateName`**: 定義上の状態名（**`executionNodeId` とは別**）。
  - **`nodeType`**, **`status`**, **`attempt`**, **`workerId`**, **`waitKey`**, **`canceledByExecution`**
  - **`input`**, **`output`**: JSON 断片（存在しない場合は省略または `null`。外部ログではマスキングを推奨）。
  - **`conditionRouting`**: 実行グラフの `conditionRouting` を API が透過的に返したもの（UI 側で再評価しない）。

通常画面の実行ビューは **`GET /v1/workflows/{id}`** と **`GET /v1/workflows/{id}/graph`** を組み合わせて UI 側で `WorkflowView` を構築する。本エンドポイントは **シーケンス指定のビュー取得**に用いる。

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
| 入力不正           | 422（`error.details` を含む場合あり） |
| 存在しない         | 404  |
| 冪等キー再利用（別リクエスト本文） | 409 。`error.code` は `IDEMPOTENCY_KEY_CONFLICT`（`POST /v1/workflows` のみ） |
| コマンド適用不可（例: Engine にワークフローが無い） | 422 。`ArgumentException` / `ApiValidationException` がマッピングされる（`ApiValidationException` は `details` 付き） |

---

## 5. UI からのアクセス

UI は `/api/core/*` 経由で Core-API にプロキシする。  
例: `/api/core/workflows/xxx` → Core-API の `/v1/workflows/xxx`（route のマッピングは UI 側で実施）。
