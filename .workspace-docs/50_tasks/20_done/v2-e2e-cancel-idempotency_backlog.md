# STV-401 / STV-402 完了記録（E2E：Cancel・冪等・409）

- Version: 1.0.0
- 更新日: 2026-04-03
- 対象: `STV-401`、`STV-402`
- 関連: `.workspace-docs/50_tasks/10_in-progress/v2-ticket-backlog.md`、`services/ui/e2e/README.md`

---

本ファイルは `v2-ticket-backlog.md` から移した**完了チケット**の受け入れ・実装参照を保持する。

---

## 実装サマリ

| ID | 実装 |
|----|------|
| STV-401 | `services/ui/e2e/core-api-real.spec.ts`（API 直叩き: Cancel → `Cancelled`）、`services/ui/e2e/core-api-ui-workflow.spec.ts`（Load → Cancel → トースト → `Cancelled`）。`CORE_API_E2E_URL` 未設定時はスキップ。 |
| STV-402 | 同 `core-api-real.spec.ts` で冪等再送・キー競合 409 + `IDEMPOTENCY_KEY_CONFLICT`。UI は `core-api-ui-workflow.spec.ts` で Cancel 応答を 409 にモックしトースト表示を検証（実 API の開始時 409 とは別経路）。 |

---

### STV-401: E2E（Cancel シーケンス）を追加

- 優先度: **P1**
- 元タスク: `remaining-tasks.md` の `4.1`
- 目的: Cancel の正常系を実環境に近い経路で保証する
- スコープ:
  - `services/ui/e2e` で Core-API 実体に対する Cancel シナリオを追加
  - workflow 開始 → Cancel → 状態遷移/ステータス確認
- 受け入れ条件:
  - Core-API 実体に対して Cancel テストが green
  - 少なくとも `cancelled` 到達（または契約上の終端）を検証
  - CI でオプション実行可能（環境変数ゲート）
- 依存: なし

---

### STV-402: E2E（冪等・409）を追加

- 優先度: **P1**
- 元タスク: `remaining-tasks.md` の `4.2`
- 目的: `X-Idempotency-Key` と競合系エラー表示を回帰防止する
- スコープ:
  - 同一キー/同一ペイロードでの再送挙動
  - 同一キー/異なるペイロードでの扱い
  - UI 側 409 表示（トースト/バナー）を E2E で確認
- 受け入れ条件:
  - API 観点で冪等挙動が契約どおり
  - 409 の UI 表示が期待どおり
  - テストは再実行安定
- 依存: `STV-401`（同じ実環境 E2E 基盤を利用）

---

## 変更履歴

| 版 | 日付 | 内容 |
|----|------|------|
| 1.0.0 | 2026-04-03 | STV-401/402 完了に伴い `10_in-progress` から移設。 |
