# データ連携契約

Version: 1.1
Project: 実行型ステートマシン
Scope: Core-Engine / Core-API / UI 間のデータ連携
Goal: 「どのデータを」「どのタイミングで」「どの形式で」連携するかを固定する

**現在の実装**: Core-Engine は `engine/`（C# ライブラリ）、Core-API は `api/`（C#）。Engine は API プロセス内で利用。UI は `/api/core/*` で API にプロキシ。以下は原則と、実装に存在する部分に沿った記述。

**Version 1.1（2026-04-12）**: Push（SSE）の現行エンドポイント・ペイロード、Graph Definition の REST パス、Write の HTTP ステータスを実装に同期した。

---

## 0. 全体像（責務の境界）

### Core-Engine（Domain Kernel・ライブラリ）

- 固定イベント・Reducer（優先順位 + normalize）・Command→Event のルール（core-engine-events-spec / core-engine-commands-spec 等で仕様化）
- **純粋ロジック**（I/O は API 層が担当）

### Core-API（C# / Integration Boundary）

- HTTP API 契約（`core-api-interface.md`）
- 永続化（EF Core）・Read API（`/v1/workflows`、`/v1/definitions`）
- **UI Push（SSE）**: `GET /v1/workflows/{id}/stream` を実装済み（`Content-Type: text/event-stream`）。グラフ JSON の変化を検知したときに `GraphUpdated` 相当の JSON を `data:` 行で送出する（詳細は §5）。**WebSocket は未実装**。

### UI（Presentation）

- Command 発行（ワークフロー開始・キャンセル・イベント）
- Read Model 表示（一覧・詳細・ExecutionGraph）

---

## 1. 連携の基本原則

1. UI は「状態を直接書き換えない」
   - UIは **Command** を送るだけ

2. UI が読むのは **Read Model**（投影）
   - UI は Event を直接読む必要はない（監査画面は例外）

3. Core は “ライブラリ” として Core-API に組み込む（現状）
   - 将来マイクロサービス化しても契約（Command / Read / Push）は同一

4. 更新は「Command → 成功レスポンス（`201` / `204` 等）→ Read Model 更新（サーバ側）→ **任意**で SSE 通知」
   - UI は成功レスポンスのあと **GET で Read Model を取得**するか、**SSE を購読**して変化を受けたうえで GET で揃える

---

## 2. データモデル（UIが依存してよい形）

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
      "nodeType": "Task|Wait|Fork|Join|...",
      "status": "IDLE|READY|RUNNING|WAITING|SUCCEEDED|FAILED|CANCELED",
      "attempt": 1,
      "workerId": "w1|null",
      "waitKey": "approval-1|null",
      "canceledByExecution": true
    }
  ]
}
```

#### UI側の最低保証

- UIは上記のフィールドのみを前提に描画できる
- ノードやグラフのレイアウト情報（座標など）は **別のGraph定義**として扱う（後述）

---

## 3. Core-API（HTTP）: Command 送信と Read 取得

### 3.1 Command API（Write）

- **現行の HTTP ステータス**: `POST /v1/workflows` は成功時 **`201 Created`**（ボディに `displayId` / `resourceId` 等）。`POST /v1/workflows/{id}/cancel`・`POST /v1/workflows/{id}/events`・`POST /v1/workflows/{id}/nodes/{nodeId}/resume` は成功時 **`204 No Content`**。
- **任意ヘッダ**:
  - **`X-Tenant-Id`**: テナントスコープ。省略時は `"default"`。作成するリソース（定義・ワークフロー）および冪等キーはこのテナントに紐づく。
- **推奨ヘッダ**:
  - `X-Idempotency-Key`（冪等・`event_delivery_dedup` の `client_event_id` 導出。省略時は dedup 保証の対象外）
  - `X-Correlation-Id`

#### 例: Cancel

`POST /v1/workflows/{id}/cancel`（現行）

- 成功時: **`204 No Content`**（レスポンス本文なし）。冪等再送時も同様に **204** で整合（重複適用は抑止される）。

#### Idempotency-Key の保持期間

- Core-API は `X-Idempotency-Key` 単位でコマンドを **`command_dedup` テーブルに保存**し、同一キー＋同一エンドポイントの再送時には**重複適用を抑止**する（成功時は初回と同じ HTTP ステータスで整合）。**レスポンス本文の完全なキャッシュ再利用**（`status_code` / `response_body` の永続化）は一部未収束（`CommandDedupRow` の TODO。`v2-modification-plan.md` §8.3 参照）。
- `command_dedup.expires_at` の **デフォルト値は `created_at + 24h`** とし、この期間を過ぎたレコードはクリーンアップ対象とする。
- **旧 `idempotency_keys` は使用しない（廃止）。冪等は `command_dedup` に一本化している。**

### 3.2 Read API（Query）

- **テナント**: 一覧・詳細はいずれも **`X-Tenant-Id`** でスコープする。省略時は `"default"`。他テナントのリソースは返さない（404 相当）。

`GET /v1/workflows/{id}`（現行）

- UIは最終状態確認にこれを使う
- コマンド直後の UI 更新は、この GET を**短い間隔でポーリング**してもよいし、**§5 の SSE** を併用してから GET で揃えてもよい

---

## 3.3 O6 契約（STV-413〜STV-415）

詳細正本: `.workspace-docs/30_specs/10_in-progress/o6-subtickets_detailed_spec.md`

### STV-413: projection 更新タイミング（現状 + 将来）

#### 現状（コマンド同期経路）

- `POST /v1/workflows` / `POST /v1/workflows/{id}/cancel` / `POST /v1/workflows/{id}/events` の各コマンドで、Engine 呼び出し後に `GetSnapshot` / `ExportExecutionGraph` を取得し、`workflows` + `execution_graph_snapshots` を更新する。うち **HTTP コマンドに付随する `event_store` 追記**は、§STV-414 の表に列挙した種別に限り、可能な範囲で **同一トランザクション**に載せる（実装準拠）。
- 将来の U1 案 C（順序付きバッチ）では、**1 バッチ = 1 トランザクション**で `event_store` への INSERT → reducer 適用 → projection 更新を行う。
- 途中失敗時はバッチ全体をロールバックし、再送時のべき等は STV-415 に従う。

#### 目標（ノード完了ごとの投影・契約）

以下は **実装未完了の目標仕様** とし、Core-API と Engine の接続（観測コールバック等）が入った段階で準拠する。

- **「ステート完了」の定義（粒度 A）**: 実行グラフ上で **`CompleteNode` が適用された直後**を、投影更新の契機とする。対象には **通常ステートに加え、Join の合成ノード**を含める（合成ノードも常に投影スナップショットに含める）。
- **event_store**: ノード完了のたびに **`event_store` へ新種別を追記しない**（当面）。`event_store` に載せるのは **外部送信・コマンドに紐づくイベントのみ**（§STV-414 の表）。ノード履歴の監査や reducer 連携が必要になった場合は **別途** 種別・ペイロード・トランザクション境界を定義する。
- **Read Model の正本**: 引き続き `workflows` + `execution_graph_snapshots`。ノード完了経路の投影更新は **`event_store` を伴わない**コミットであり得る。UI・SSE の正本は Read API / 投影済み JSON とする方針（§5.1、`AGENTS.md`）は変えない。

#### 高負荷時: API 内キュー（ドラフト）

ノード完了が高頻度でも DB 負荷とロック競合を抑えるため、Engine から Core-API への **観測コールバック**で「投影を進めよ」シグナルを受け、**API プロセス内のキュー**で受け口を一本化する方針を採用してよい。

- **狙い**: エンジンスレッドを DB I/O で長くブロックしない、同一 `workflow_id` の書き込みを直列化してデッドロックを減らす、**ワークフロー単位の併合（coalesce）**（短時間に複数ノードが完了したとき **最新スナップショット 1 回分**だけ永続化する）を許容し得る。
- **キューで満たすべき性質（仕様レベル・確定）**
  - **単調性**: DB に反映される `execution_graph_snapshots` は、**過去にコミットされたノード完了結果より「巻き戻った」内容にならない**こと（併合しても、エンジンが既に記録した完了事実を失わない）。
  - **HTTP コマンドとの整合**: `POST …/cancel` および `POST …/events` 等、**投影と `event_store` を同一トランザクションで更新する必要がある経路**では、当該 `workflow_id` のキューを **ドレイン**したうえで Engine 変異と DB を行う、または等価な **ロック順序**を実装で固定し、コマンド完了時点の投影がコールバック経路と競合しないこと。
- **キュー深さ・溢れ時（仕様レベル・確定案）**
  - **ワークフロー単位**: 未フラッシュの投影要求は **高々 1 スロット**（ダーティフラグ等）。同一 `workflow_id` に対する連続コールバックは **置き換え**のみとし、待ち行列が無限に伸びないこと。
  - **プロセス全体**: ワーカーへ渡す **有界**なグローバル待ち行列（容量は実装で設定可能とし、既定値は例として **16384** 件程度のスロットを想定。数値は負荷試験で調整）。**溢れたときはブロック**（有界チャネルの「満杯なら書き込み側が待つ」挙動）。これが本節での **バックプレッシャー**の意味とする（生産者＝コールバック側が消費に追いつくまで進まない）。
  - **ドロップ**: **禁止**。完了済みノードの事実を「未永続のまま捨てる」ことは、Read Model 正本方針と両立しない。
  - **極端な詰まり**: ブロックが長時間続く場合は **可観測性**（ログ・メトリクス）で検知し、**水平スケール**や DB 側チューニングで対処する。コールバック側に **短い警告閾値用タイムアウト**を付ける場合は、タイムアウト後の挙動（例: ログのみで引き続き待機 vs プロセス健全性のためのフェイル）を実装コメントで明示し、**黙ってドロップしない**こと。
- **併合の時間窓（仕様レベル・確定案）**
  - **ワークフロー単位のデバウンス** `ProjectionFlushDebounceMs`（設定可能）を設ける。**既定値は 50 ms**（推奨レンジ **0〜250 ms**）。§5.1 の SSE が約 2 秒であるため、UI の追従性より **DB 書き込み回数の削減**を主目的とする。
  - **`0`**: 時間によるまとめは行わない。コールバックのたびに（ワークフロー単位スロット経由で）**できるだけ早く**フラッシュ要求を出す。負荷は増え得る。
  - **`N > 0`**: 同一 `workflow_id` で **最後のコールバックから N ms 動きがなければ** 1 回の DB 書き込みにまとめる（デバウンス）。短時間の分岐バーストに有効。
  - 併合後も **単調性**（上記）を壊さないこと。
- **Graceful shutdown（仕様レベル・確定案）**
  - **必須**: アプリケーションの **正常停止シグナル**を受けたら、グローバル待ち行列と **進行中のフラッシュ**を **ドレイン**する（新規の Engine 開始は受け付けない／既存のみ処理、の順序は実装で固定）。
  - **タイムアウト**: ドレインは **ホストのシャットダウンタイムアウト**（例: ASP.NET Core の `HostOptions.ShutdownTimeout`、既定 5 秒を超える場合は **アプリ設定で延長**）の範囲で **best effort**。タイムアウト内に完了しなかった項目は **構造化ログ**に残し、プロセスは終了し得る（異常終了時と同様、**最後にコミットされた投影まで**が正本。未フラッシュ分は失われ得るため、運用では **termination grace を十分に**確保することが推奨）。
  - **Kubernetes 等**: `terminationGracePeriodSeconds` をシャットダウンタイムアウトより **大きく**し、ドレインが収束する余裕を持たせる。

#### SSE（フェーズ 1）

- **`GET /v1/workflows/{id}/stream`** のサーバ挙動（約 **2 秒**間隔の投影 JSON 比較）は **現状のまま**とする。ノード粒度で投影が更新されても、UI への `GraphUpdated` は **最大約 2 秒の遅れ**を許容する（§5.1）。

### STV-414: event_store 対応表（現行）

| event_store `type` | 発火契機（HTTP） | payload（概要） |
| ------------------ | ---------------- | --------------- |
| `WorkflowStarted` | `POST /v1/workflows` 成功 | `definitionId`, `tenantId` |
| `WorkflowCancelled` | `POST /v1/workflows/{id}/cancel` 成功 | `tenantId` |
| `EventPublished` | `POST /v1/workflows/{id}/events` 成功 | `tenantId`, `name` |

**ノード完了（Engine 内部）**: 当面 **`event_store` には載せない**（§STV-413 目標）。将来、監査・BI・リプレイ要件が出た場合に `NODE_*` 等を **別途** 定義する。

### STV-415: 再送べき等（現行 Core-API + 将来バッチ）

**現行（コマンド同期経路）**

- HTTP コマンドの再送は従来どおり `command_dedup`（`X-Idempotency-Key` + endpoint）で初回レスポンスを再現する。
- `POST /v1/workflows/{id}/cancel` と `POST /v1/workflows/{id}/events` では、さらに **`event_delivery_dedup`** テーブルで `(tenant_id, workflow_id, client_event_id)` を一意とし、先行 `RECEIVED` → 成功時 `APPLIED`（失敗時 `FAILED`）で配送冪等の正本を DB に持つ。
- `client_event_id` は `X-Idempotency-Key` から解決する（RFC 4122 UUID 形式のキーはその値をそのまま用いる。それ以外は実装既定の決定論的導出）。
- Engine は同一 `client_eventId` の再適用で `AlreadyApplied` を返し得る。API はそれに応じて投影更新は行いつつ `event_store` は **insert-skip**（クライアントイベント単位の重複追記抑止）で DB と収束させる。
- `event_delivery_dedup` への `RECEIVED` 先行 INSERT は一時障害時に段階的バックオフで再試行する（設定上限・タイムアウト方針は実装の `EventDeliveryRetryOptions` に従う）。
- **API プロセス再起動などで Engine に当該ワークフローが存在しない**（`GetSnapshot` が null）とき、投影が非終端（`Running` 等）または終了済みであってもコマンド適用は行わず **`ArgumentException`（HTTP 422）** とする。投影を `Unknown` へ壊す更新を防ぐ。

**将来（案 C バッチ）**

- バッチ再送では `batchId`（UUID）を付与し、バッチ内イベントは全成功時のみ commit、失敗時は全体 rollback のうえ同一 `batchId` 再送を許容する。
- メトリクスは将来対応。

---

## 4. Graph 定義（UI描画に必要な静的データ）

Read Model は「状態」だけなので、UIは別途「構造」を知る必要がある。

### 4.1 Graph Definition（推奨API）

**現行**: `GET /v1/graphs/{graphId}`（`graphId` は定義の **display_id**。`X-Tenant-Id` でスコープ）。UI からはプロキシ経由で `GET /graphs/{graphId}` として呼ぶ構成でもよい（`core-api-interface.md` に準拠）。

```json
{
  "graphId": "hello",
  "nodes": [
    { "nodeId": "start", "nodeType": "Start", "label": "Start" },
    { "nodeId": "task-1", "nodeType": "Task", "label": "Task A" }
  ],
  "edges": [{ "from": "start", "to": "task-1" }],
  "ui": {
    "layout": "dagre|manual",
    "positions": { "start": { "x": 100, "y": 80 } }
  }
}
```

#### 重要

- Execution Read Model（状態）と Graph Definition（構造）は分離する
- UIは **構造×状態** を合成して描画する

---

## 5. Realtime Push（SSE / WS）

SSE でサーバから UI へ「再取得のきっかけ」を送ると、ポーリングだけより変化が追いやすい。**WebSocket は現行未実装**。

### 5.1 現行: SSE エンドポイント

- **`GET /v1/workflows/{id}/stream`**（`{id}` は **display_id または resource_id（UUID）** のいずれか。`X-Tenant-Id` は他 Read API と同様。UI からは `EventSource` 等でプロキシ URL に接続し、テナントは `docs/ui-api-auth-tenant-config.md` / `ui-push-api-spec.md` に従う）
- 応答ヘッダ: `Content-Type: text/event-stream`、`Cache-Control: no-cache, no-transform` 等（中間プロキシのバッファリング抑止用ヘッダを付与）
- **サーバ側挙動（実装準拠）**: 約 **2 秒**間隔で投影済みグラフ JSON を読み、内容の SHA-256 が前回から変わったときだけ **1 行の `data:`** を書き込む。接続はクライアントが閉じるまで継続（長時間ポーリング型 SSE）。

#### 5.1.1 `data:` 行の JSON 形（`GraphUpdated`）

Core-API は **Execution Read Model 全体**ではなく、**グラフスナップショット由来の patch.nodes** を送る（実装クラス: `WorkflowStreamService`）。

```json
{
  "type": "GraphUpdated",
  "executionId": "<workflow display_id>",
  "patch": {
    "nodes": {}
  }
}
```

- `executionId`: ワークフローの **display_id**（UI の URL 等と一致しやすい）
- `patch.nodes`: `WorkflowViewMapper.MapGraphPatchNodes` の結果（ノード状態のマップ。詳細は API 実装および `WorkflowViewDto` 系マッピングに従う）

**UI の推奨**: 本イベントを受けたら **`GET /v1/workflows/{id}`** で Read Model を再取得し、画面を確定状態に揃える（SSE は「いま読み直す価値がある」通知であり、正本は Read API とする方針は §3.3 / `AGENTS.md` の Read-model authority と整合）。

### 5.2 将来拡張: 統一 Push イベント（UI 向け・未実装）

UI が扱いやすいよう、Engine のイベントをそのまま流すのではなく、**Read Model 更新通知**だけを送る形を推奨する。現行 SSE は §5.1 の **`GraphUpdated`** に限定されている。

#### UI-UPDATE（推奨・契約上の理想形・未実装）

```json
{
  "type": "EXECUTION_UPDATED",
  "executionId": "ex-1",
  "version": 12,
  "changed": ["status", "nodes.task-1.status"],
  "at": "2026-02-28T00:00:00Z"
}
```

UIは受けたら `GET /v1/workflows/{id}` を再取得する（最小・堅牢）。

#### UI-PATCH（中級：差分で高速化・未実装）

```json
{
  "type": "EXECUTION_PATCH",
  "executionId": "ex-1",
  "version": 12,
  "patch": [
    { "op": "replace", "path": "/status", "value": "CANCELED" },
    { "op": "replace", "path": "/nodes/task-1/status", "value": "CANCELED" }
  ],
  "at": "2026-02-28T00:00:00Z"
}
```

> 現行は **§5.1 の `GraphUpdated`** で十分なことが多い。`EXECUTION_*` 型へ寄せる場合は別チケットで SSE ペイロードを拡張する。

---

## 6. UI の更新戦略（推奨）

### 6.1 ポーリングのみ（SSE 未使用）

- Command 送信（`201` / `204`）
- UI は短い間隔で `GET /v1/workflows/{id}` をポーリング（例: 0.5s→1s→2s などバックオフ）
- 状態が終端または一定時間で停止

### 6.2 SSE 併用（現行で利用可能）

- Command 送信（`201` / `204`）
- **`GET /v1/workflows/{id}/stream`** を `EventSource` 等で購読し、`GraphUpdated` を受信したタイミングで `GET /v1/workflows/{id}` を再取得
- 即時に UI が追従しやすい（§5.1 の約 2 秒間隔に合わせ、過剰な GET を抑えられる）

---

## 7. エラーとUI表現（最小ルール）

- 409 Conflict: 状態競合（例: cancelRequested後のresume）
- 422: 入力不正、および Core-API が `ArgumentException` にマッピングするコマンド拒否（例: Engine にワークフローが無く `POST .../cancel` や `POST .../events` を適用できない場合）
- 404: execution/node不在

UIは 409 を「操作できない理由」として表示できるよう、
Core-APIはエラーレスポンスに以下を入れる。

```json
{
  "error": {
    "code": "COMMAND_REJECTED",
    "message": "Execution is cancel-requested",
    "details": {
      "cancelRequestedAt": "..."
    }
  }
}
```

---

## 8. シーケンス（現状と将来）

### 8.1 現状（ポーリングのみ）

```text
UI -> Core-API: POST /v1/workflows/{id}/cancel
Core-API -> UI: 204 No Content
UI -> Core-API: GET /v1/workflows/{id} (poll)
Core-API -> UI: status 等
```

### 8.2 現状（SSE 併用・実装済み）

```text
UI -> Core-API: POST /v1/workflows/{id}/cancel
Core-API -> UI: 204 No Content
UI -> Core-API: GET /v1/workflows/{id}/stream (SSE、並行で開く)
Core-API -> UI: data: {"type":"GraphUpdated", ...}
UI -> Core-API: GET /v1/workflows/{id}
Core-API -> UI: Read Model（確定状態）
```

### 8.3 将来（EXECUTION_UPDATED 型の統一イベント）

`EXECUTION_UPDATED` / JSON Patch 型の Push（§5.2）を SSE に載せる場合のシーケンスは、実装導入時に本節へ追記する。

---

## 9. バージョニング（互換性）

- Read Model は後方互換を守る（フィールド追加は可、削除/意味変更は不可）
- Push payload も同様
- 破壊的変更が必要なら `v2` エンドポイントを追加する
