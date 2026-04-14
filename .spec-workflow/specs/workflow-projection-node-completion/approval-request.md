# 承認依頼: spec `workflow-projection-node-completion`

## メタデータ

| 項目 | 内容 |
|------|------|
| **spec 識別子** | `workflow-projection-node-completion` |
| **依頼日** | 2026-04-14 |
| **対象** | Core-API + Engine 境界（実行グラフ投影のノード粒度更新） |
| **実装チケット** | 未割当（承認後に `v2-ticket-backlog.md` 等へ起票してよい） |
| **機械可読メタデータ** | [approval-status.json](./approval-status.json) |

---

## 依頼概要

`docs/statevia-data-integration-contract.md` **§3.3 STV-413** に記載した **目標仕様**（ノード完了ごとの `execution_graph_snapshots` 更新、**event_store にはノード完了を載せない**、**API 内キュー**と併合・ドレイン・Graceful shutdown、**SSE は約 2 秒のまま**）について、Spec Workflow 上の **[requirements.md](./requirements.md)** の内容で問題ないか、**設計・実装フェーズに進む承認**をお願いします。

本依頼に紐づく **design.md / tasks.md は作成済み**。承認済みのため、tasks に従って実装フェーズへ進める。

---

## 承認対象ドキュメント（読む順の推奨）

1. **要件**: [requirements.md](./requirements.md) — Requirement 1〜6、非機能、Out of Scope  
2. **契約正本（承認の根拠・準拠先）**: `docs/statevia-data-integration-contract.md` の §3.3（現状・目標・高負荷時・SSE）、§3.3 STV-414（ノード完了は event_store に載せない）  
3. **関連追跡**: `.spec-workflow/specs/o6-subtickets-detailed/requirements.md`（STV-413 との位置づけ）

---

## 承認者チェックリスト

承認者は以下を確認のうえ、欄外または PR / Issue で **承認 / 差し戻し** を明示してください。

- [ ] **粒度 A**: `Complete` 適用直後を契機とし、Join 合成ノードを含む定義が明確である  
- [ ] **event_store**: ノード完了を当面追記しない方針が、監査要件とチーム認識で許容される  
- [ ] **キュー**: ワークフロー 1 スロット・グローバル有界・満杯時ブロック・ドロップ禁止が過大でない  
- [ ] **デバウンス既定 50 ms**（0〜250 ms 可変）が運用・UI（SSE 2 秒）と矛盾しない  
- [ ] **HTTP コマンド**: Cancel/Events 前のドレインまたは等価なロック順序が説明可能である  
- [ ] **Graceful shutdown**: ドレイン best effort とタイムアウト・ログの方針が許容される  
- [ ] **SSE**: フェーズ 1 で約 2 秒据え置きでよい

---

## 承認欄

| 承認者 | 役割 | 判定 | 日付 | コメント |
|--------|------|------|------|----------|
| プロジェクト | 依頼者代理（開発セッション） | ☑ 承認 | 2026-04-14 | requirements を承認し、design/tasks を作成済み |

**差し戻し時**は、`requirements.md` または `docs/statevia-data-integration-contract.md` §3.3 のどれをどう修正すべきかを具体的に記載してください。

---

## 承認後の作業（実装担当）

1. 本ファイルの **承認欄**および [approval-status.json](./approval-status.json) の `status` / `approvedAt` を更新する。  
2. [requirements.md](./requirements.md) 先頭の **承認状態**を「承認済み」に更新する。  
3. 必要に応じて **design.md** / **tasks.md** を追加し、実装チケットを起票する。  

---

## 参照

- `docs/statevia-data-integration-contract.md`
- `.spec-workflow/specs/workflow-projection-node-completion/requirements.md`
- `.spec-workflow/specs/o6-subtickets-detailed/requirements.md`
