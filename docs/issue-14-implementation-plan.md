# Issue #14 実装方針: 実行履歴タイムライン/リプレイ表示

## 概要

時系列で Execution の推移を追える UI を追加する。

- **履歴タイムライン**: イベント一覧を時系列表示し、時点を選択可能にする
- **時点選択による状態再現**: 選択した時点までの状態でグラフ/ノード一覧を表示
- **現在状態にワンクリックで戻る**: リプレイ表示から最新状態に復帰

---

## 現状の整理

### バックエンド (core-api)

| 要素 | 内容 |
|------|------|
| **状態の取得** | `GET /executions/:id` で「現在の状態」のみ（投影テーブル `executions` + `node_states`） |
| **イベント** | `events` テーブルに `seq`, `execution_id`, `type`, `occurred_at`, `payload` 等で保存 |
| **EventStore** | `listSince(executionId, afterSeq, limit)` のみ。先頭から全件取得は `afterSeq=0` で可能（ページング要） |
| **Reducer** | ドメインの `reduce(ExecutionState, EventEnvelope)` でイベントを適用して状態を更新。リプレイは「空状態から seq 1..N まで適用」で実現可能 |
| **SSE** | `/executions/:id/stream` で新規イベントを配信。ストリーム用の `mapPersistedEventToStreamEvent` で UI 向けイベント形式に変換済み |

### フロント (ui)

| 要素 | 内容 |
|------|------|
| **実行状態** | `useExecution(executionId)` が `GET /executions/:id` + SSE で常に「最新」の `ExecutionDTO` を保持 |
| **グラフ** | `useGraphData(execution, graphDefinition)` が `execution.nodes` からグラフを生成。**表示は常に `execution` に依存** |
| **ストリーム適用** | `applyExecutionStreamEvent(current, event)` で `ExecutionDTO` に SSE 形式イベントを適用可能（UI 側でリプレイ用に流用可） |

### ギャップ

- **時点指定の状態取得**: API に「指定 seq 時点の状態」がない
- **イベント一覧**: タイムライン表示用の「イベント一覧（seq / type / occurredAt）」API がない
- **UI**: 表示用の `execution` を「最新」と「リプレイ時点」で切り替える仕組みがない

---

## 実装方針

### 1. API 拡張 (core-api)

#### 1.1 イベント一覧（タイムライン用）

- **エンドポイント**: `GET /executions/:executionId/events`
- **クエリ**: 省略可。必要なら `?limit=500` など（デフォルト 200 程度）
- **レスポンス**: 時系列順のイベント一覧。各要素は **SSE と同じ形式**（`type`, `executionId`, `at`, `patch`/`to`/`nodeId` 等）にし、フロントの `applyExecutionStreamEvent` をそのまま使えるようにする。

  ```ts
  // 例
  { events: [ { type: "GraphUpdated", executionId, patch: { nodes: [...] }, at }, ... ] }
  ```

- **実装**: `EventStore.listSince(executionId, 0, limit)` で先頭から取得し、既存の `mapPersistedEventToStreamEvent` でストリーム形式に変換して返す。  
  イベント数が多い場合はページング（`?afterSeq=&limit=`）を検討。

#### 1.2 時点指定の状態取得（リプレイ用）

- **エンドポイント**: `GET /executions/:executionId/state?atSeq=:seq`
- **仕様**: `atSeq` まで（その seq を含む）のイベントをドメインの `reduce` で畳み込み、得られた `ExecutionState` を現在の `GET /executions/:id` と同じ JSON 形状（`executionId`, `status`, `graphId`, `nodes: [...]` 等）で返す。
- **実装**:
  - EventStore に `listUpTo(executionId, maxSeq)` または `listSince(executionId, 0, maxSeq)` のループで seq ≤ atSeq のイベントを取得
  - PersistedEvent → EventEnvelope に変換（DB に `actor_kind`, `actor_id`, `schema_version` があるので組み立て可能）
  - 空の `ExecutionState` から `reduce` を順に適用
  - 返却用に `ExecutionState` → 既存 GET レスポンス形式に変換

**代替案**: フロントで「イベント一覧を取得して先頭から `applyExecutionStreamEvent` で畳み込み」でも実現可能。その場合は 1.1 のみ追加し、1.2 は後回しにしてもよい（フロント負荷とイベント数次第）。

---

### 2. UI 実装 (ui)

#### 2.1 データ

- **タイムライン用イベント一覧**: `GET /executions/:id/events` を叩く hook（例: `useExecutionEvents(executionId)`）。返却を `ExecutionStreamEvent[]` として保持。
- **表示用 execution**:
  - 通常: 既存どおり `useExecution(executionId)` の `execution`（常に最新）
  - リプレイ時: 選択した時点までのイベントを `applyExecutionStreamEvent` で畳み込んだ `ExecutionDTO` を別 state で保持し、グラフ/リストはこれを参照する。

#### 2.2 履歴タイムライン UI

- 実行詳細画面（同一 execution を表示している画面）に「履歴」または「タイムライン」セクションを追加。
- イベント一覧を時系列（`at` または `seq`）で表示。各項目で「その時点の状態を表示」を選択可能にする。
- 選択時: 上記「表示用 execution」をリプレイ用に切り替え（API で `?atSeq=` を叩くか、フロントでイベント畳み込み）。

#### 2.3 グラフ/リストとの連動

- 表示用 execution が「最新」か「リプレイ時点」かで、`useGraphData(displayExecution, graphDefinition)` に渡す execution を切り替える。
- 既存の `useExecution` は「最新」取得用にそのまま使い、リプレイ用の `displayExecution` だけ別 state で管理する。

#### 2.4 「現在に戻る」

- ボタンまたはリンクで「リプレイ時点」をクリアし、表示用 execution を再度「最新」（`useExecution` の `execution`）に戻す。

---

## 受け入れ基準との対応

| 基準 | 対応 |
|------|------|
| 時点切替でノード状態が変わる | 表示用 execution を「atSeq までリプレイした結果」にし、`useGraphData`/ノードリストに渡す |
| 現在状態にワンクリックで戻れる | 「現在に戻る」で表示用 execution を最新に戻す |

---

## 実装順序（推奨）

1. **core-api**: `GET /executions/:id/events` を追加（PersistedEvent をストリーム形式で返す）。
2. **core-api**: （必要なら）`GET /executions/:id/state?atSeq=` を追加。またはまずはフロントでイベント畳み込みで対応。
3. **ui**: `useExecutionEvents(executionId)` と、イベント配列から「atSeq まで」の execution を計算するユーティリティ。
4. **ui**: 実行詳細にタイムライン UI を追加（イベント一覧表示 + 時点選択）。
5. **ui**: 表示用 execution の切り替え（最新 vs リプレイ）と「現在に戻る」ボタン。
6. **結合テスト**: 時点切替でグラフ/リストが変わること、現在に戻るで最新に復帰することを確認。

---

## 依存・注意

- **Dependencies**: #1, #2 を前提とする（実行一覧・実行詳細の基盤が既にある）。
- **EventStore**: イベント数が非常に多い実行では、イベント一覧のページングや「直近 N 件」に絞る検討が必要。
- **SSE との整合**: ストリーム形式イベントと履歴 API のイベント形式を揃えることで、フロントの `applyExecutionStreamEvent` を共通利用する。
