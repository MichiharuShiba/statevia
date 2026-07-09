# Core API 契約（HTTP）

| 項目 | 値 |
| --- | --- |
| 種別 | Specification |
| Version | 1.8 |
| 更新日 | 2026-05-28 |
| 関連 | [reference/api-openapi.md](../reference/api-openapi.md), [concepts/platform.md](../concepts/platform.md) |

---

## Normative 要約

- **MUST**: Runtime API（`/v1/definitions` / `/v1/executions`）は Principal 必須（JWT または `X-Api-Key`）+ `X-Tenant-Id`。
- **MUST**: 定義版は immutable。`PUT /v1/definitions/{id}` は新版 INSERT のみ（既存版の上書き禁止）。
- **MUST**: 実行は開始時の `definition_version_id` に固定する。
- **MUST**: 入力検証失敗は **422**、`error.code = VALIDATION_ERROR`（[data-integration.md](data-integration.md) §7）。
- **SHOULD**: ミューテーションに `X-Idempotency-Key` を付与する。
- **SHOULD**: Start 時の `input` / state `output` を一覧・GET で既定返却しない（IO-14）。

運用叙述・SSE・Read-model の注意は本書 § 以降。エンドポイントの機械可読な正本は OpenAPI（[api-openapi.md](../reference/api-openapi.md)）。

---

Core-API（C#、`service/api/`）の HTTP 契約。実装に準拠。

**Version 1.8（2026-05-28）**: Runtime API（`/v1/definitions` / `/v1/executions`）を **Principal 必須**に変更。JWT または API キー（`X-Api-Key`）で `ITenantContext.PrincipalId` を解決し、`X-Tenant-Id` 単独は 401。

**Version 1.7（2026-05-26）**: ExecutionSpace 命名統一（7a〜7d）。HTTP **`/v1/executions`**、永続 **`executions`** / **`execution_events`**、Engine **`IExecutionEngine.Start(..., input)`**、DTO **`ExecutionResponse`** / **`ExecutionViewDto`** に同期。旧 `/v1/executions` エイリアスなし。

**Version 1.6（2026-05-20）**: immutable 定義版（`definitions` / `definition_versions`）と実行時の `definitionVersionId` 固定を追記。`PUT /v1/definitions/{id}` は **版追加（publish）** として記述。Start リクエストに `definitionVersion` / `definitionVersionId` を追加。

**Version 1.5（2026-05-11）**: 実行グラフ JSON の **`edges[*].from` / `edges[*].to`**（実行ノード ID）と、`GET …/state` の **`ExecutionViewDto.nodes[*]`**（`executionNodeId` と `stateName` の分離、運用メタ・入出力）を契約本文に明記。

- **Base path**: `/v1`
- **Policy**: 終端の優先順位はエンジン内で保証
- **Style**: RESTful

## OpenAPI / Scalar（機械可読な契約）

| 項目 | 内容 |
| --- | --- |
| スキーマ・操作の正本 | Swashbuckle 生成の `/swagger/v1/swagger.json`、およびリポジトリ内 [`service/api/openapi/core-api-v1.openapi.json`](../../service/api/openapi/core-api-v1.openapi.json) |
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
| `executions.definition_version_id` | 実行開始時に固定した版への FK（execution correctness） |

**禁止事項:**

- 既存 `definition_versions` 行の `source_yaml` / `compiled_json` を **上書きしない**（更新は新版 INSERT のみ）。
- publish トランザクションで **`latest_version` のみ先行コミット** しない（同一 ReadCommitted tx 内で version INSERT → latest 更新）。
- Start 時に **mutable な定義行**や **latest だけ**から `compiled_json` を取得しない（必ず解決した **version 行**の `compiled_json` を使用）。

**移行期（フェーズ 1a）:** `definitions.project_id` は NULL 可。テナント境界は `definitions.tenant_id` で担保（`projects` / `project_accesses` 導入後に認可 truth を移行）。

**フェーズ 1b（projects / project_accesses）:**

| 対象 | 役割 |
| --- | --- |
| `project_accesses` + オーナーテナント | **認可 truth**（`reader` / `executor` / `publisher` / `admin`） |
| `projects.visibility` | **discoverability ヒント**（認可には使わない） |
| `definitions.project_id` | NOT NULL。`UNIQUE(project_id, slug)` |

定義取得・publish・Start は `ITenantContext.TenantInternalId` と `project_accesses` で評価する。Reader 未満は 404（存在秘匿）、Reader のみが Start した場合は 403（`PROJECT_ACCESS_DENIED`）。

**publish トランザクション:** `ICoreTransactionExecutor.ExecuteReadCommittedAsync` の 1 コールバック内で `definition_versions` INSERT → `definitions.latest_version` 更新。

---

## 1. エンドポイント一覧

| メソッド | パス                      | 説明                          |
| -------- | ------------------------- | ----------------------------- |
| GET      | /v1/health                | 死活                          |
| POST     | /v1/definitions           | 定義登録                      |
| PUT      | /v1/definitions/{id}      | 定義更新（displayId または UUID） |
| DELETE   | /v1/definitions/{id}      | 定義の catalog 論理削除       |
| POST     | /v1/definitions/{id}/restore | 削除済み定義の復元         |
| GET      | /v1/definitions           | 定義一覧                      |
| GET      | /v1/definitions/{id}      | 定義取得                      |
| GET      | /v1/definitions/schema/nodes | nodes 入力スキーマ取得     |
| GET      | /v1/actions/schema        | 登録 action 一覧（descriptor 概要） |
| GET      | /v1/actions/schema/index  | Playground 向け軽量 index |
| GET      | /v1/actions/schema/{actionId} | action の input/output schema + UI metadata |
| GET      | /v1/graphs/{graphId}      | Graph Definition（nodes/edges） |
| POST     | /v1/executions             | 実行開始                      |
| GET      | /v1/executions             | 実行一覧                      |
| GET      | /v1/executions/{id}        | 実行取得                      |
| GET      | /v1/executions/{id}/graph  | 実行グラフ（JSON）取得        |
| GET      | /v1/executions/{id}/state  | 状態ビュー（`atSeq` クエリ必須） |
| GET      | /v1/executions/{id}/events  | event_store タイムライン（`afterSeq`, `limit`） |
| GET      | /v1/executions/{id}/stream  | SSE（グラフ変化を `GraphUpdated` で送出） |
| POST     | /v1/executions/{id}/cancel | キャンセル                    |
| POST     | /v1/executions/{id}/events | イベント発行（例: Wait 再開） |
| POST     | /v1/executions/{id}/nodes/{nodeId}/resume | ノード再開（body: `resumeKey`） |

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

### 2.1.2 catalog 論理削除

**DELETE /v1/definitions/{id}**

- `id`: displayId または UUID
- 成功: **204 No Content**（`definitions.deleted_at` を **UTC** で設定）。既に削除済みの場合も冪等 **204**。
- 存在しない／他テナント／削除済みの operational invisibility: **404**。
- `definition_versions`・既存 `executions` は物理削除しない。

### 2.1.3 catalog 復元

**POST /v1/definitions/{id}/restore**

- 削除済み定義のみ対象。成功: **200 OK** + `DefinitionResponse`（`deletedAt` は含めない）。
- 未削除定義: **409**（`error.code`: `STATE_CONFLICT`）。
- 同一 project 内で active な別定義が同 slug を保持: **422**（`slug` フィールドに details）。
- 存在しない／他テナント: **404**。

**operational invisibility:** soft delete 後、単体 GET・一覧（既定）・新規 Start・`GET /v1/graphs/{graphId}` は **404**（存在秘匿。410 は不採用）。削除前に開始した execution の GET / events / graph snapshot は **200 維持**。

### 2.2 定義一覧

**GET /v1/definitions**

- **`?limit=&offset=&name=&sortBy=&sortOrder=&includeDeleted=`**（`limit` 必須）: 200 OK、`PagedResult<DefinitionResponse>`（`items`, `totalCount`, `offset`, `limit`, `hasMore`）。
  - `name`: 名前の部分一致
  - `sortBy`: `createdAt` / `name`（未指定時は `createdAt`）
  - `sortOrder`: `asc` / `desc`（未指定時は `desc`）
  - `limit`: 1〜500（必須）、`offset`: 0 以上（省略時 0）
  - `includeDeleted`: `true` のとき削除行を含む。各 item に **`deletedAt`**（UTC）のみ追加返却（通常 GET では出さない）
- `limit` 未指定・不正: **422**

**移行:** 一覧取得は `?limit=N&offset=M` を必須とする。全件が必要な場合は `limit=500` と `hasMore` を用いてページを繰り返す。

### 2.3 定義取得

**GET /v1/definitions/{id}**

- `id`: displayId または UUID
- Response: 200 OK で 1 件（`displayId`, `resourceId`, `name`, `latestVersion`, `createdAt`, `updatedAt`, **`yaml`**（**最新版**の保存済みソース））。catalog 上削除済み・存在しない・他テナントは **404**（operational invisibility）。

### 2.4 Graph Definition（構造）

**GET /v1/graphs/{graphId}**

- `graphId`: 定義の **displayId** または UUID（実装コメント上は display_id 解決）。
- **X-Tenant-Id**: 任意。省略時 `"default"`。
- Response: 200 OK、`GraphDefinitionResponse`（`graphId`, `nodes[]`, `edges[]` 等。詳細は実装の `GraphDefinitionResponse`）。
- 404: 定義が存在しない、catalog 上削除済み、または当該テナントに無い（operational invisibility）。

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

### 2.6 Action Schema API

Builtin / Module action の **input/output JSON Schema** と **UI metadata** を返す。Playground の schema 駆動フォームと Edge `when.path` 補完の源泉。

**GET /v1/actions/schema**

- Response: 200 OK — `{ "items": [ { "actionId", "displayName", "version", "category?", "hasSchema" } ] }`

**GET /v1/actions/schema/index**

- Playground 向け軽量 index — `{ "items": [ { "actionId", "displayName", "version" } ] }`（publication 登録済みのみ）

**GET /v1/actions/schema/{actionId}**

- Response: 200 OK — `descriptor` / `schema`（`inputSchema`, `outputSchema`, `schemaVersion`）/ `uiMetadata` を分離 DTO で返す
- 404: 未登録 actionId、または publication 未登録

**認可**: `definitions.read`（§4.1.2.1）。**IO-14**: レスポンスに定義 YAML の機微値は含めない（schema 契約のみ）。

**Compiler 連携**: 定義 publish 時、action 状態の `input` map は publication の `inputSchema` に対し検証される（422 `details` に `state`, `actionId`, `jsonPath` — 機微値は含めない）。ルートフラットに加え、ネスト `type: object` を再帰検証する（フェーズ F2）。`ship.address` ドットキーと `ship: { address: ... }` ネスト map は同等。正規化衝突は 422。

---

### 3.1 実行開始

**POST /v1/executions**

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
- `input`: 任意の JSON 値（省略可）。初期状態へ渡される
- Engine 投入は解決した **version 行の `compiled_json`**（同一版の `source_yaml` で executor を復元）
- 永続化: `executions.definition_version_id` に開始版を必ず保存（Start は ReadCommitted 1 tx: `executions` + snapshot + `event_store` + dedup）
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

### 3.2 実行一覧

**GET /v1/executions**

- **`?limit=&offset=&status=&name=&definitionId=&sortBy=&sortOrder=`**（`limit` 必須）: 200 OK、`PagedResult<ExecutionResponse>`。
  - `status`: `executions.status` 列と**完全一致**
  - `name`: `display_id` の部分一致（`Guid` 形式入力時は `execution_id` 完全一致も許容）
  - `definitionId`: Definition の displayId または UUID
  - `sortBy`: `updatedAt` / `displayId`（未指定時は `updatedAt`）
  - `sortOrder`: `asc` / `desc`（未指定時は `desc`）
  - `limit`: 1〜500（必須）、`offset`: 0 以上（省略時 0）
- `limit` 未指定・不正: **422**

### 3.3 実行取得

**GET /v1/executions/{id}**

- Response: 200 OK、**一覧と同一形の `ExecutionResponse`**（UI の ExecutionDTO と整合）。404 は未存在。

### 3.4 実行グラフ取得

**GET /v1/executions/{id}/graph**

Response: 200 OK、Content-Type: application/json。`execution_graph_snapshots` に保存された **ExecutionGraph と同形の JSON** を返す。404 は未存在。

- JSON キー命名は **camelCase**。
- トップレベルは **`nodes`**, **`edges`**（ExecutionGraph のシリアライズ形）。**HTTP** では `execution_graph_snapshots` の行が無い場合は **404**（`ExecutionService.GetGraphJsonAsync`）。エンジン API `ExportExecutionGraph` がメモリにインスタンスを持たないときは **`{}`** 文字列を返し得るが、それは in-process 観測用であり、Read API の正本ではない（`AGENTS.md` Read-model authority）。
- **`nodes[*].nodeId`**: ランタイム実行ノード ID（短いランダム ID）。**定義**の `GET /v1/graphs/{graphId}` における **`nodes[*].nodeId`（状態名ベースのキャンバス ID）とは別**。
- **`nodes[*].stateName`**: 定義上の状態名。UI はマージ時に `stateName` および実行エッジで対応付ける（`docs/specifications/execution/execution-graph.md` §7）。
- **`edges[*].from` / `edges[*].to`**: いずれも **`nodes[*].nodeId`** を指す。旧キー `fromNodeId` / `toNodeId` は用いない。
- **`edges[*].type`**: `EdgeType` の数値（`Next` 0 など）。`Join`（2）では合流元から Join 合成ノードへ **複数辺** が立ち得る。
- 条件遷移を評価したノードは `conditionRouting` を含む。
  - 主要キー: `fact`, `resolution`, `matchedCaseIndex`, `caseEvaluations`, `evaluationErrors`
  - `resolution` は `linear` / `matched_case` / `default_fallback` / `no_transition`
- ノードの **`input` / `output` / `attempt` / `workerId` / `waitKey` / `canceledByExecution` / `nodeType`** などの詳細は `docs/specifications/execution/execution-graph.md` §4 を正とする。

**IO-14**: グラフ JSON に含まれる `input` / `output` は機微情報になり得る。一覧 `GET /v1/executions` 等では既定で返さない方針は `AGENTS.md` の Input/Output exposure policy に従う。

### 3.5 状態ビュー（UI）

**GET /v1/executions/{id}/state?atSeq={seq}**

- **atSeq**: 必須（long）。`event_store` のシーケンスに基づく状態ビュー（`ExecutionViewDto`）。リプレイ用途。
- Response: 200 OK。404 は未存在。
- 現行実装は **スナップショット近似**であり、`atSeq` 時点の厳密な過去状態の完全再構成を保証するものではない（運用上の注意は UI 文言・将来の強化チケットに委ねる）。
- **`ExecutionViewDto`** は UI の `ExecutionView` に近い camelCase。`displayId`, `resourceId`, `graphId`, `status`, `startedAt`, `updatedAt`, `cancelRequested`, `restartLost`, **`nodes`**。
- **`ExecutionViewDto.nodes[*]`**（`ExecutionViewNodeDto`）の主なフィールド:
  - **`executionNodeId`**: 実行グラフの **`nodeId`** と一致させる識別子（試行単位の実行ノード）。
  - **`stateName`**: 定義上の状態名（**`executionNodeId` とは別**）。
  - **`nodeType`**, **`status`**, **`attempt`**, **`workerId`**, **`waitKey`**, **`canceledByExecution`**
  - **`input`**, **`output`**: JSON 断片（存在しない場合は省略または `null`。外部ログではマスキングを推奨）。
  - **`conditionRouting`**: 実行グラフの `conditionRouting` を API が透過的に返したもの（UI 側で再評価しない）。

通常画面の実行ビューは **`GET /v1/executions/{id}`** と **`GET /v1/executions/{id}/graph`** を組み合わせて UI 側で `ExecutionView` を構築する。本エンドポイントは **シーケンス指定のビュー取得**に用いる。

### 3.6 イベントタイムライン（Read）

**GET /v1/executions/{id}/events**

- クエリ: **`afterSeq`**（既定 0）、**`limit`**（既定 500、上限は実装に従う）。
- Response: 200 OK、`ExecutionEventsResponseDto`（タイムライン行の列挙）。404 は未存在。

### 3.7 SSE（グラフ変化の Push）

**GET /v1/executions/{id}/stream**

- Response: **`200`**、`Content-Type: text/event-stream`。本文は SSE の **`data:`** 行に JSON（`type: GraphUpdated` 等）。接続維持型（サーバは約 2 秒周期で投影グラフを比較し、変化時のみ `data:` を書き込む）。
- 認証は現行未実装。テナントは **`X-Tenant-Id`**（UI から `EventSource` でヘッダが付けられない場合は `docs/guides/ui-auth-tenant-config.md` のクエリ経由を参照）。
- 詳細ペイロードは `docs/specifications/data-integration.md` §5.1 を正とする。
- 404: 実行未存在。

### 3.8 キャンセル

**POST /v1/executions/{id}/cancel**

- **X-Idempotency-Key**: 任意だが推奨。同一キー＋同一リクエストの再送は `command_dedup` により初回と同じ結果（通常 204）を返す。キーは `event_delivery_dedup` の `client_event_id` 導出にも使われる（詳細は `docs/specifications/data-integration.md` の STV-415）。
- Response: 204 No Content。エンジンで Cancel を適用し、projection を更新。
- Engine に当該実行が無い（例: API 再起動直後）場合は **422**（`ArgumentException`。データ連携契約のセクション7）。

### 3.9 イベント発行（Write）

**POST /v1/executions/{id}/events**

Request:

```json
{
  "name": "string"
}
```

- `name`: イベント名（例: Wait の resume 用）。必須。不正時は 400。
- **X-Idempotency-Key**: 任意だが推奨。再送・重複排除の扱いはキャンセルと同様（`command_dedup` + `event_delivery_dedup` / `client_event_id`）。
- Response: 204 No Content。
- Engine に当該実行が無い場合は **422**（キャンセルと同様）。

### 3.10 ノード再開

**POST /v1/executions/{id}/nodes/{nodeId}/resume**

Request（JSON、省略可）:

```json
{
  "resumeKey": "string"
}
```

- **X-Idempotency-Key**: 任意だが推奨（キャンセル・イベント発行と同様の冪等・配送抑止）。
- Response: 204 No Content。422 / 404 の扱いは実装および `docs/specifications/data-integration.md` §7 に従う。

---

## 4. 共通

### 4.1 ヘッダ

- **Content-Type**: application/json（Body がある場合）
- **Authorization**: `Bearer <JWT>`（ログイン後）。Runtime API（`/v1/definitions` / `/v1/executions`）では Principal 必須。
- **X-Api-Key**: API キー認証。`api_keys`（prefix + hash）照合で Principal を解決する。
- **X-Tenant-Id**: 移行専用。JWT あり時は **`tenant_key` と一致必須**。不一致は **403**（`TENANT_HEADER_MISMATCH`）。Runtime API では単独指定を許可せず **401**。
- **X-Idempotency-Key**: 任意。`POST /v1/executions` では `definitionId + input` を含むリクエストハッシュで冪等キーを分離する（同一キーでも input が異なれば別リクエスト扱い）。

### 4.1.1 認証 API（初版）

**POST /v1/auth/login**

Request:

```json
{
  "tenantKey": "default",
  "email": "user@example.com",
  "password": "string"
}
```

- Response: 200 OK、`{ "accessToken", "expiresAt", "tenantId", "tenantKey", "principalId" }`
- 失敗: 401（資格情報不正）、403（テナント停止）

**GET /v1/auth/me**

- **Authorization** 必須。
- Response: 200 OK、`{ "tenantId", "tenantKey", "principalId", "email", "isTenantAdmin" }`

### 4.1.3 テナント管理者 API（初版）

いずれも **JWT 必須**かつ **`is_tenant_admin` の Principal のみ**（403 `FORBIDDEN`）。パスプレフィックス: `/v1/admin`。

| メソッド | パス | 概要 |
| --- | --- | --- |
| GET | `/permissions` | 権限カタログ（`permission_definitions`） |
| GET | `/users` | ユーザー一覧 |
| POST | `/users` | ユーザー作成（`email`, `password`, `displayName?`, `isTenantAdmin`, `groupIds?`） |
| PATCH | `/users/{userId}` | 有効化/無効化・管理者フラグ（`isActive?`, `isTenantAdmin?`） |
| GET | `/groups` | グループ一覧 |
| POST | `/groups` | グループ作成（`name`） |
| GET | `/groups/{groupId}` | グループ詳細（メンバー・権限キー） |
| PUT | `/groups/{groupId}/members` | メンバー置換（`userIds`） |
| PUT | `/groups/{groupId}/permissions` | 権限置換（`permissionKeys`。`tenant.admin` は不可） |
| GET | `/api-keys` | API キー一覧（平文なし。`keyPrefix` / `allowedScopes` / `expiresAt` / `lastUsedAt`） |
| POST | `/api-keys` | API キー発行（`name`, `allowedScopes`, `expiresAt?`）。応答の `plainKey` は **一度だけ** |
| DELETE | `/api-keys/{apiKeyId}` | API キー失効（紐づく Principal を無効化） |
| GET | `/modules` | Action Module の load catalog 一覧（`AdminModuleListItemDto[]`） |

JWT クレーム: `tenant_id`（内部 UUID）、`tenant_key`、`principal_id` / `sub`。詳細は `docs/specifications/platform/security-runtime.md`。

### 4.1.4 内部向け Module API（運用）

テナント管理者 JWT 必須（403 `FORBIDDEN`）。HTTP からの module 配置は想定しない（filesystem 信頼境界）。reload は **CLI install 後の明示反映**用。

| メソッド | パス | 概要 |
| --- | --- | --- |
| POST | `/internal/modules/reload` | `ModuleHost` へ discover / load を再実行（204） |

Response（`GET /v1/admin/modules` の 1 件）: `moduleId`, `name`, `version`, `status`, `sha256`, `sourceLabel?`, `loadedAtUtc`, `message?`, `entryAssemblyPath`。`sourceLabel` は取得元 Source を示し、filesystem 由来は未設定、OCI 由来は `oci:{registry}/{repository}:{reference}`（`CompositeModuleSource` が複数 Source を集約）。

### 4.1.2 Runtime API の認証要件

- **保護対象**: `/v1/definitions`、`/v1/executions`、`/v1/graphs` 配下。
- **必須**: `ITenantContext.PrincipalId` が解決済みであること（JWT または `X-Api-Key`）。
- **拒否**: `X-Tenant-Id` のみ（Bearer / API キーなし）は **401**（`UNAUTHORIZED`）。
- **除外パス**: `/v1/auth/login`、`/v1/health`、`/swagger/*`、`/scalar/*`。

### 4.1.2.1 Runtime API の global permission 認可

Principal 解決後、サービス層で **semantic permission key** を評価する（project 認可 `project_accesses` と併用）。

| 操作 | permission key |
| --- | --- |
| GET `/v1/definitions*`、`/v1/graphs/*`、`/v1/definitions/schema/nodes`、`/v1/actions/schema*` | `definitions.read` |
| POST/PUT `/v1/definitions` | `definitions.write` |
| GET `/v1/executions*`（一覧・詳細・graph・state・events・stream） | `executions.read` |
| POST start / cancel / publish / resume | `executions.write` |

- **JWT**: グループ権限を Live 展開（`ExpandPrincipalPermissionKeysAsync`）。`is_tenant_admin` は全 catalog key を持つ。
- **API キー**: `effective = 展開許可 ∩ allowed_scopes` の交差結果のみ（`ITenantContext.EffectivePermissionKeys`）。
- **不足時**: **403**（`PERMISSION_DENIED`）。

### 4.2 JSON 命名ポリシー（実装準拠）

- Core-API が返す JSON は原則 **camelCase** を採用する。
- `GET /v1/executions/{id}/graph`（ExecutionGraph JSON）と、定義コンパイル由来のデバッグ JSON（`compiledJson`）は camelCase で統一済み。

### 4.3 ステータスコード

| 状況               | HTTP |
| ------------------ | ---- |
| 成功（作成）       | 201  |
| 成功（取得・一覧） | 200  |
| 成功（No Content） | 204  |
| 入力不正           | 422（`error.details` を含む場合あり） |
| 存在しない         | 404  |
| 冪等キー再利用（別リクエスト本文） | 409 。`error.code` は `IDEMPOTENCY_KEY_CONFLICT`（`POST /v1/executions` のみ） |
| コマンド適用不可（例: Engine に実行が無い） | 422 。`ArgumentException` / `ApiValidationException` がマッピングされる（`ApiValidationException` は `details` 付き） |
| 認証失敗・資格情報不正 | 401 。`error.code` は `UNAUTHORIZED` 等 |
| テナント停止・ヘッダ不一致 | 403 。`TENANT_SUSPENDED` / `TENANT_ARCHIVED` / `TENANT_HEADER_MISMATCH` |

調べ物: [error-codes.md](../reference/error-codes.md) · [permission-keys.md](../reference/permission-keys.md)

---

## 5. UI からのアクセス

UI は `/api/core/*` 経由で Core-API にプロキシする。  
例: `/api/core/executions/xxx` → Core-API の `/v1/executions/xxx`（route のマッピングは UI 側で実施）。
