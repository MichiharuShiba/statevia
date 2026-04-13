# 承認依頼: spec `ui-playground`（UI Playground）

## メタデータ

| 項目 | 内容 |
|------|------|
| **spec 識別子** | `ui-playground` |
| **依頼日** | 2026-04-12 |
| **対象フェーズ** | Phase 3（`.workspace-docs/40_plans/10_in-progress/v2-roadmap.md`） |
| **実装チケット** | 未割当（承認後に `v2-ticket-backlog.md` へ起票してよい） |
| **機械可読メタデータ** | [approval-status.json](./approval-status.json) |

---

## 依頼概要

Statevia の **UI Playground**（定義 YAML の登録・ワークフロー開始・実行操作・可視化を一体で行う開発者向け UI）について、**spec-workflow 上の要件・設計・タスク**の内容で問題ないか、**実装フェーズ（P3.0〜）に進む承認**をお願いします。

## 承認対象ドキュメント（読む順の推奨）

1. **要件**: [requirements.md](./requirements.md) — Requirement 1〜7、非機能、Out of Scope  
2. **設計要約**: [design.md](./design.md) — アーキテクチャ決定、mermaid、SSE 方針  
3. **詳細ワイヤー・API 表**: `.workspace-docs/30_specs/10_in-progress/ui-playground-design.md`  
4. **実装タスク**: [tasks.md](./tasks.md) — P3.0 / P3.1 / P3.2  

**HTTP 契約の正本**（承認対象外・参照のみ）: `docs/core-api-interface.md`、`docs/statevia-data-integration-contract.md`

---

## 承認者チェックリスト

承認者は以下を確認のうえ、欄外または PR / Issue で **承認 / 差し戻し** を明示してください。

- [ ] **スコープ**: MVP は `/playground` ルート・定義登録・開始・既存コンポーネント再利用に収まっており、Out of Scope（認証本格、`states` フル等）が過大でない  
- [ ] **契約整合**: 要件が現行 Core-API（`POST /v1/definitions`、validate 専用なし、204/201 等）と矛盾しない  
- [ ] **Read の正**: GET を正とし、SSE は通知＋GET 確定の方針が `AGENTS.md` / データ連携契約と整合する  
- [ ] **タスク分割**: P3.0 → P3.1 → P3.2 の順でリスクが許容できる  

---

## 承認欄

| 承認者 | 役割 | 判定 | 日付 | コメント |
|--------|------|------|------|----------|
| プロジェクト | 依頼者代理（開発セッション） | ☑ 承認 | 2026-04-12 | P3.0 実装に着手 |

**差し戻し時**は、`requirements.md` / `design.md` / `ui-playground-design.md` のどれをどう修正すべきかを具体的に記載してください。

---

## 承認後の作業（実装担当）

1. 本ファイルまたは Issue で **承認記録**を残す。  
2. `requirements.md` 先頭の **承認状態**を「承認済み」に更新する。  
3. `tasks.md` の **P3.0** を `[-]` にし、実装を開始する。  

---

## 参照

- `.workspace-docs/30_specs/10_in-progress/v2-ui-spec.md`
- `.spec-workflow/steering/product.md`
