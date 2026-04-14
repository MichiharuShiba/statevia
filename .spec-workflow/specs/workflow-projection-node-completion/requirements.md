# Requirements: ノード完了ごとの実行グラフ投影（API 内キュー）

> **承認状態**: **承認済み**（2026-04-14）— [approval-request.md](./approval-request.md) / [approval-status.json](./approval-status.json)

## Introduction

ワークフロー実行中、**実行グラフ上でノードが完了するたび**に Read Model（`workflows` / `execution_graph_snapshots`）を更新し、UI が SSE / GET で追従できるようにする。  
Engine は DB を知らない前提のまま、**観測コールバック → Core-API 内キュー → 永続化**で高負荷時の書き込みを制御する。

**契約正本（実装の準拠先）**: `docs/statevia-data-integration-contract.md` の **§3.3 STV-413（目標・高負荷時・SSE）** および **§3.3 STV-414（ノード完了は event_store に載せない）**。本書は Spec Workflow 上の **要件・受入の追跡**用に構造化する。

**関連**: O6 の STV-413 拡張として `.spec-workflow/specs/o6-subtickets-detailed/requirements.md` の Requirement 1 と整合する。差分は「ノード完了経路の具体要件」を本 spec に集約する。

## Alignment with Product Vision

実行状態の **正本を DB 投影に置く**方針（`AGENTS.md`、STV-416）を維持したまま、**ノード粒度**で投影を進め、ダッシュボードのグラフと運用の見え方を一致させる。`event_store` は外部コマンド由来のイベントに限定し、スコープを明確に保つ。

## Requirements

### Requirement 1 — 投影契機（粒度 A）

**User Story:** As a **UI 利用者**, I want **各実行ノードの完了が投影に反映される**こと, so that **実行グラフが実行進行に追従して見える**。

#### Acceptance Criteria — Requirement 1

1. WHEN **実行グラフ上で `CompleteNode` が適用される**（通常ステート完了時）THEN **Core-API は最終的に `execution_graph_snapshots` を当該時点のエンジン状態と整合するよう更新する**。
2. WHEN **Join の合成ノードが完了する** THEN **そのノードもスナップショット JSON に含まれ、省略されない**。
3. WHEN **投影のみを更新する**（ノード完了コールバック経路）THEN **`event_store` に新種別を追記しない**（当面。将来は別 spec）。

### Requirement 2 — Engine と API の境界

**User Story:** As a **実装者**, I want **Engine が永続層に依存しない**こと, so that **モジュール境界（`structure.md`）が保たれる**。

#### Acceptance Criteria — Requirement 2

1. WHEN **ノード完了を Core-API に伝える** THEN **Engine は `IWorkflowEngine` 等の公開面越しに、登録可能な観測（コールバック／`IObserver` 相当／イベント）のみを用いる**（具体 API 名は design で確定）。
2. WHEN **コールバックが呼ばれる** THEN **コールバック本体は同期 I/O を行わず、フラッシュ要求を API 側キューへ委譲する**（長時間ブロックを避ける）。

### Requirement 3 — API 内キューと併合

**User Story:** As a **運用者**, I want **高頻度完了でも DB が過負荷にならない制御がある**こと, so that **スループットと整合性のバランスが取れる**。

#### Acceptance Criteria — Requirement 3

1. WHEN **同一 `workflow_id` に複数の完了通知が届く** THEN **未フラッシュ要求はワークフロー単位で高々 1 スロット**とし、**置き換え**により待ち行列が無限に伸びないこと。
2. WHEN **グローバル待ち行列が満杯になる** THEN **生産者はブロックする**（バックプレッシャー）。**完了事実をドロップしてはならない**。
3. WHEN **グローバル待ち行列の容量を設定する** THEN **既定は契約ドキュメントの例（16384 スロット程度）に沿い、設定で変更可能**であること。
4. WHEN **併合（デバウンス）を行う** THEN **`ProjectionFlushDebounceMs` が **0〜250 ms** の範囲で設定可能であり、**既定は 50 ms** であること。
5. WHEN **併合後に DB へ書き込む** THEN **過去コミットよりグラフが「巻き戻る」内容にならない**（単調性）。

### Requirement 4 — HTTP コマンドとの整合

**User Story:** As a **API 利用者**, I want **Cancel / Events 等のコマンド結果と投影が矛盾しない**こと, so that **Read Model の信頼性が保たれる**。

#### Acceptance Criteria — Requirement 4

1. WHEN **`POST …/cancel` または `POST …/events` を処理する** THEN **当該 `workflow_id` のノード完了用キューがドレイン済みであるか、等価なロック順序により、コールバック経路の投影と競合しない**こと。
2. WHEN **HTTP コマンドが `event_store` と投影を同一トランザクションで更新する** THEN **既存の STV-415 / dedup 方針と矛盾しない**こと。

### Requirement 5 — Graceful shutdown

**User Story:** As a **インフラ担当**, I want **正常停止時に未フラッシュを可能な限り吐き出す**こと, so that **計画停止後の投影のずれが最小になる**。

#### Acceptance Criteria — Requirement 5

1. WHEN **アプリケーションが正常停止する** THEN **グローバル待ち行列および進行中フラッシュをドレインする処理が走る**こと。
2. WHEN **シャットダウンタイムアウトに達する** THEN **未完了項目は構造化ログに残し、黙ってドロップとみなさない**こと（プロセス終了はあり得る）。
3. WHEN **運用ドキュメントを参照する** THEN **`terminationGracePeriodSeconds` と `ShutdownTimeout` の関係が推奨として記載されている**こと（契約 §3.3 に準拠）。

### Requirement 6 — SSE（フェーズ 1）

**User Story:** As a **プロダクトオーナー**, I want **既存 SSE 契約を維持する**こと, so that **UI 改修を必須にしない**。

#### Acceptance Criteria — Requirement 6

1. WHEN **`GET /v1/workflows/{id}/stream` が動作する** THEN **約 2 秒間隔の投影 JSON 比較による `GraphUpdated` 送出という挙動は変更しない**（§5.1）。
2. WHEN **ノード粒度で投影が更新される** THEN **UI 側は引き続き「最大約 2 秒の遅れ」を許容する**（契約どおり）。

## Non-Functional Requirements

### テスト

- Engine 単体: コールバックが意図した順序・回数で発火する（モック）。
- Core-API: キュー溢れ時のブロック、デバウンス 0 / 50ms、ドレインと HTTP コマンドの競合がないことの単体または統合テストを **少なくとも 1 シナリオずつ**用意する。

### 可観測性

- キュー滞留・ドレイン未完了・ブロック時間超過を **ログまたはメトリクス**で検知できること（詳細キー名は design / 実装で確定）。

### ドキュメント

- 実装完了時点で **`docs/statevia-data-integration-contract.md`** の「目標」「確定案」と実装の差分がないこと、または差分は契約側を更新して解消すること。

## Out of Scope（本 spec では扱わない）

- `event_store` への `NODE_*` 種別追加（将来別要件）。
- SSE のポーリング間隔短縮や Push モデル変更（フェーズ 2 以降）。
