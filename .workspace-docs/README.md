# .workspace-docs 運用ガイド

このディレクトリは、仕様・計画・タスク・メモを「状態別」で管理するための作業領域です。

新規ファイルの命名・種別の固定語彙は `.workspace-docs/20_discussion/docs-format-unification.md` を参照する。

---

## 1. クイックリンク（v2）

- 全体方針: `.workspace-docs/40_plans/10_in-progress/v2-readme.md`
- 改修計画: `.workspace-docs/40_plans/10_in-progress/v2-modification-plan.md`
- 残タスク一覧: `.workspace-docs/50_tasks/10_in-progress/v2-remaining-tasks.md`
- チケット一覧: `.workspace-docs/50_tasks/10_in-progress/v2-ticket-backlog.md`
- nodes → states 変換（Phase 5）: `.workspace-docs/30_specs/10_in-progress/v2-nodes-to-states-conversion-spec.md`

---

## 2. 基本構成

```text
.workspace-docs/
  README.md
  migration-plan.md
  00_inbox/
  10_notes/
  20_discussion/
  30_specs/
    10_in-progress/
    20_done/
    30_archived/
  40_plans/
    10_in-progress/
    20_done/
    30_archived/
  50_tasks/
    10_in-progress/
    20_done/
    30_archived/
```

---

## 3. 置き場所ルール

- `00_inbox/`: 一時置き（未分類）。現時点では維持し、運用実績を見て再検討
- `10_notes/`: 開発者の思想・個人メモ・調査記録の場
- `20_discussion/`: 開発者と AI の対話・合意形成の場（合意前の議論を記録）
- `30_specs/`: 要件・契約・設計仕様
- `40_plans/`: 実行計画・設計判断・方針
- `50_tasks/`: 実行可能なタスク分解、チケット、検証手順

状態はファイル名ではなくフォルダで管理します。

- `10_in-progress`: まだ更新が続く文書
- `20_done`: 現時点の正本として確定した文書
- `30_archived`: 参照のみ（廃止、旧版、置換済み）

---

## 4. 更新ルール

- 文書の状態が変わったら、対象ファイルを状態フォルダへ移動する
- `archived` は削除しない（履歴参照のため）
- `50_tasks/20_done` へ移す条件:
  - 受け入れ条件を満たす
  - `remaining-tasks` など親タスク表が更新済み
- `30_specs/20_done` へ移す条件:
  - 実装と齟齬がない
  - 参照先ドキュメントとの不整合がない

---

## 5. 推奨運用

- 週次で `inbox` を空にする
- スプリント開始時に `50_tasks/10_in-progress` を見て対象を確定する
- スプリント終了時に `50_tasks/20_done` と `40_plans/20_done` を更新する
- 運用ルールの見直しは `20_discussion/` で先に合意してから正本へ反映する
