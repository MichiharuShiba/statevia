# O6 懸念分解仕様

- Version: 1.1.0
- 更新日: 2026-04-10
- 対象: `v2-modification-plan.md` の C2 / C7 / C11 / C13 / C14 の分解
- 関連: `.workspace-docs/50_tasks/10_in-progress/v2-ticket-backlog.md`, `.spec-workflow/specs/concern-o6-decomposition/requirements.md`

---

## 1. 目的

`O6`（懸念の解消）を、実装・仕様化に着手できる粒度へ分解する。  
本書は `STV-410` の成果物として、各懸念の現状、未確定点、次アクション、優先度、依存を整理する。

---

## 2. 懸念棚卸し（C2 / C7 / C11 / C13 / C14）

| 懸念 | 分類 | 現状の決定事項 | 未確定点 | 参照 |
|------|------|----------------|----------|------|
| C2 | 一貫性 | U1 でイベント処理方針（案 C、順序付きバッチ）が確定。 | コマンド戻り値経路とコールバック経路で projection 更新手順を同一仕様として固定する定義が不足。 | `v2-modification-plan.md` 8.2、`v2-u1-event-ordering-and-transactions.md` |
| C7 | 仕様 | U1/U2 で event_store の役割と保存方針は確定。 | Engine イベント種別と event_store `type` の対応表、発火タイミング、必須 payload の仕様が未整備。 | `v2-modification-plan.md` 8.2、`docs/core-events-spec.md` |
| C11 | 永続化 | バッチ単位トランザクション方針は確定。 | コールバック失敗時の再送で event 重複を防ぐ `event_id` べき等仕様、リトライ上限、失敗時観測性が未確定。 | `v2-modification-plan.md` 8.2、`v2-u1-event-ordering-and-transactions.md` |
| C13 | Engine | reducer は Engine 配置（U7）で確定。 | `GetSnapshot` をメモリ値で返すか reducer 出力と整合させるか、デバッグ時の期待値をどちらに寄せるか未確定。 | `v2-modification-plan.md` 8.2、`v2-u7-reducer-placement.md` |
| C14 | Phase 5 | nodes/states 判別（U10）は確定。 | nodes 固有要素（`onError` / `timeout` / `controls` / `output`）の段階的対応順と、未対応時の契約が未確定。 | `v2-modification-plan.md` 8.2、`v2-nodes-to-states-conversion-spec.md` |

---

## 3. サブチケット定義（STV-413〜STV-418）

| ID | タイトル | 優先度 | 依存 | 受け入れ条件（一行） |
|----|----------|--------|------|----------------------|
| STV-413 | C2: projection 更新タイミングの統一仕様化 | P1 | - | コマンド戻り値経路/コールバック経路の更新順序・トランザクション境界を 1 仕様に統一し、更新図を含めて文書化される。 |
| STV-414 | C7: Engine イベントと event_store 対応表の策定 | P1 | STV-413 | 主要イベントの `eventType`・発火契機・payload 必須項目・保存先が対応表で定義される。 |
| STV-415 | C11: コールバック失敗時の再送べき等仕様化 | P1 | STV-413, STV-414 | `event_id` 重複排除、リトライ戦略、失敗時ログ/メトリクス方針を含む運用可能な仕様が定義される。 |
| STV-416 | C13: GetSnapshot と reducer 出力の整合方針決定 | P2 | STV-414 | API/Engine/デバッグで参照するスナップショットの正を定義し、差分検証方針を決定する。 |
| STV-417 | C14: nodes 未対応要素の段階導入計画策定 | P2 | STV-413 | nodes 固有フィールドの対応優先順位と、未対応時のエラー契約が仕様化される。 |
| STV-418 | O6 横断: 懸念対応ロードマップ統合 | P2 | STV-413, STV-414, STV-415, STV-416, STV-417 | O6 サブチケットの依存順・スプリント割当・完了判定が `v2-ticket-backlog` と整合する形で更新される。 |

---

## 4. 優先度・依存の整理

- まず `STV-413` を完了し、更新順序の正本を固定する。
- 次に `STV-414` でイベント語彙を確定する。
- `STV-415` は `STV-414` の語彙を前提に失敗再送を確定する。
- `STV-416` と `STV-417` は並行可能だが、影響範囲を減らすため `STV-413` 完了後に着手する。
- `STV-418` は横断整理として最後に実施する。

---

## 5. バックログ連携

- 親チケット: `STV-410`（本書作成で完了）
- サブチケット: `STV-413`〜`STV-418`（未着手）
- 追跡先: `.workspace-docs/50_tasks/10_in-progress/v2-ticket-backlog.md`

---

## 6. 詳細仕様（STV-413〜STV-418）

各サブチケットの受け入れ可能な仕様は、次を正本とする。

- `.workspace-docs/30_specs/10_in-progress/o6-subtickets_detailed_spec.md`

---

## 変更履歴

| 版 | 日付 | 内容 |
|----|------|------|
| 1.1.0 | 2026-04-10 | §6 を追加し、`o6-subtickets_detailed_spec.md` へリンク。 |
| 1.0.0 | 2026-04-09 | C2/C7/C11/C13/C14 の棚卸しと、`STV-413`〜`STV-418` のサブチケット定義を追加。 |
