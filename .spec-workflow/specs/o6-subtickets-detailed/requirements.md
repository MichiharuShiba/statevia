# Requirements: O6 サブチケット詳細仕様（STV-413〜STV-418）

## Introduction

`v2-modification-plan.md` の懸念 **C2 / C7 / C11 / C13 / C14** に対応する実行チケット（`STV-413`〜`STV-418`）について、**現状実装の正**と **U1 将来実装時の契約**を一文書で固定する。  
詳細な表・シーケンス図・用語定義の正本は `.workspace-docs/30_specs/10_in-progress/o6-subtickets_detailed_spec.md` とする。本 spec は spec-workflow 上の追跡・承認用に要件を構造化する。

**紐づくチケット**: `v2-ticket-backlog.md` の `STV-413`〜`STV-418`。親: `STV-410`（`concern-o6-decomposition`）。

## Alignment with Product Vision

イベント順序・projection・Read Model の一貫性は、運用ログ・UI・監査の信頼性に直結する。未確定点を残さず契約化することで、Engine 拡張（非同期コールバック等）時の手戻りを抑える。

## Requirements

### Requirement 1 — STV-413（C2: projection 更新タイミング）

**User Story:** As a **アーキテクト**, I want **コマンド同期経路と将来のコールバック経路で projection / event_store の更新ルールが一文書で定義される**こと, so that **実装レビューで判断基準が共有される**。

#### Acceptance Criteria — Requirement 1

1. WHEN **現行 Core-API** THEN **Start / Cancel / Publish の各 HTTP コマンドにおけるトランザクション境界・isolation・projection 更新順が表で文書化される**。
2. WHEN **U1 コールバック（案 C）を将来導入する** THEN **1 バッチ = 1 トランザクションで INSERT → reducer → projection が同一仕様として記述される**。
3. WHEN **STV-413 を完了とみなす** THEN **上記が `docs/` または契約ドキュメントに転記され、modification-plan C2 と矛盾しない**。

### Requirement 2 — STV-414（C7: event_store 対応表）

**User Story:** As a **保守者**, I want **`EventStoreEventType` と HTTP 契機・payload が対応表で正本化される**こと, so that **新種別追加時の影響範囲が明確になる**。

#### Acceptance Criteria — Requirement 2

1. WHEN **参照する** THEN **`WorkflowStarted` / `WorkflowCancelled` / `EventPublished` の発火契機と payload 概要が表で定義される**。
2. WHEN **Engine 内部イベント語彙を将来 event_store に載せる** THEN **「載る／載らない／TBD」が行として表に残る**（TBD 可）。
3. WHEN **STV-414 を完了とみなす** THEN **契約ドキュメントまたは正本へのリンクが `docs` から辿れる**。

### Requirement 3 — STV-415（C11: 再送べき等）

**User Story:** As a **運用者**, I want **コールバック再送時のべき等・リトライが仕様化される**こと, so that **二重適用や無限リトライを防げる**。

#### Acceptance Criteria — Requirement 3

1. WHEN **イベント再送** THEN **`clientEventId` 等による重複排除方針が記述される**。
2. WHEN **バッチ再送（案 C）** THEN **`batchId` と rollback / 再試行の関係が記述される**。
3. WHEN **リトライ上限超過** THEN **ログ・将来メトリクス・手動介入の方針が記述される**。

### Requirement 4 — STV-416（C13: GetSnapshot と reducer）

**User Story:** As a **API 利用者**, I want **HTTP が返す実行状態の「正」が DB projection であることが明文化される**こと, so that **Engine メモリと混同しない**。

#### Acceptance Criteria — Requirement 4

1. WHEN **`GET /v1/workflows/{id}` 等** THEN **正は `workflows` / `execution_graph_snapshots` であると文書化される**。
2. WHEN **`GetSnapshot` をデバッグ参照する** THEN **in-process ビューであり、非同期導入後は最終永続と限らない旨が文書化される**。
3. WHEN **STV-416 を完了とみなす** THEN **`AGENTS.md` または `docs/` に上記が反映される**。

### Requirement 5 — STV-417（C14: nodes 段階導入）

**User Story:** As a **定義作者**, I want **nodes の未対応フィールドの優先順位と MVP の拒否契約が固定される**こと, so that **ロードマップに沿って拡張できる**。

#### Acceptance Criteria — Requirement 5

1. WHEN **MVP** THEN **`onError` / `timeout` / `output` / `controls` 等は現行どおり拒否される**。
2. WHEN **段階導入** THEN **優先順位（onError → timeout → output → controls の提案）が文書化される**。
3. WHEN **STV-417 を完了とみなす** THEN **`v2-nodes-to-states-conversion-spec.md` の将来拡張と整合する参照がある**。

### Requirement 6 — STV-418（横断統合）

**User Story:** As a **PM**, I want **バックログ・計画書・仕様正本の参照が一貫する**こと, so that **O6 の完了判定ができる**。

#### Acceptance Criteria — Requirement 6

1. WHEN **STV-418 を完了とみなす** THEN **`v2-ticket-backlog.md` と `v2-modification-plan.md` が本仕様群を指す**。
2. WHEN **STV-413〜417 がすべて完了** THEN **残る作業は実装チケットに落ちている**。

## Non-Functional Requirements

### Clarity

- 用語は U1 / U7 の既存ドキュメントと整合させ、差分は「現状実装」と「将来契約」で明示する。

## Out of Scope

- コールバック経路の**実装**（別チケット）。
- `STV-412`（マスキング外部テンプレート）。

## References

- `.workspace-docs/30_specs/10_in-progress/o6-subtickets_detailed_spec.md`（詳細正本）
- `.workspace-docs/30_specs/10_in-progress/o6-concerns_decomposition_spec.md`
- `.workspace-docs/50_tasks/20_done/v2-u1-event-ordering-and-transactions.md`
- `.workspace-docs/50_tasks/20_done/v2-u7-reducer-placement.md`
