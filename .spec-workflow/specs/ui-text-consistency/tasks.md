# Tasks: UI表示文言の整理と表記ゆれ統一

**spec 名**: `ui-text-consistency`  
**要件**: `requirements.md`  
**設計**: `design.md`

---

- [x] **T1** — 用語集と統一対象の確定
  - File: `.spec-workflow/specs/ui-text-consistency/requirements.md`, `.spec-workflow/specs/ui-text-consistency/design.md`
  - 内容: ナビ名称、操作語、状態語、エラープレフィクスの統一対象を固定し、`design.md` の `Confirmed Mapping Table` を正本として確定する
  - 目的: 実装前に統一ルールを明確化し、判断ぶれを防ぐ
  - _Leverage: `services/ui/app/layout.tsx`, `services/ui/app/components/layout/PageState.tsx`, `services/ui/app/lib/errors.ts`_
  - _Requirements: Requirement 1, Requirement 2, Requirement 3, Requirement 4_
  - _Definition of Done: 用語集と対象範囲が requirements/design に反映され、`Confirmed Mapping Table` の全行がレビュー済みである（完了: 2026-04-26）_

- [x] **T2** — 共通文言カタログの追加
  - File: `services/ui/app/lib/uiText.ts`
  - 内容: `navigation/actions/pageState/errorPrefixes` の文言定義を実装し、`design.md` の `Confirmed Mapping Table` の語彙をキー単位で対応付ける
  - 目的: 文言の単一参照点を作る
  - _Leverage: 既存ハードコード文言_
  - _Requirements: Requirement 1, Requirement 2, Requirement 3_
  - _Definition of Done: `uiText.ts` が追加され、`Confirmed Mapping Table` の全行に対応するキーまたは適用方針（対象外理由含む）が記録されている（完了: 2026-04-26）_

- [x] **T3** — 共通部品への適用
  - File: `services/ui/app/layout.tsx`, `services/ui/app/components/layout/PageState.tsx`, `services/ui/app/lib/errors.ts`, `services/ui/app/components/layout/ListPagination.tsx`
  - 内容: ナビ・状態表示・エラー文言・ページネーション文言をカタログ参照へ置換する
  - 目的: 全画面へ波及する文言を先に統一する
  - _Leverage: `services/ui/app/lib/uiText.ts`_
  - _Requirements: Requirement 1, Requirement 2, Requirement 3_
  - _Definition of Done: 上記共通部品で表記ゆれが解消される（完了: 2026-04-26）_

- [x] **T4** — 主要画面への展開
  - File: `services/ui/app/dashboard/DashboardPageClient.tsx`, `services/ui/app/definitions/DefinitionsPageClient.tsx`, `services/ui/app/workflows/WorkflowsPageClient.tsx`, `services/ui/app/components/execution/ExecutionDashboard.tsx`
  - 内容: 露出の高い画面から段階適用し、同義語の混在を解消する
  - 目的: 利用者が最も目にする領域で効果を先行させる
  - _Leverage: `services/ui/app/lib/uiText.ts`_
  - _Requirements: Requirement 1, Requirement 2_
  - _Definition of Done: 主要画面で統一語彙が適用される（完了: 2026-04-26）_

- [x] **T5** — テスト更新と回帰確認
  - File: `services/ui/tests/components/**`, `services/ui/tests/lib/errors.test.ts`, `services/ui/e2e/**`
  - 内容: 文言アサーションを更新し、テスト実行で回帰を確認する
  - 目的: 文言統一の継続的な品質担保を行う
  - _Leverage: 既存 UI テスト基盤_
  - _Requirements: Requirement 1, Requirement 2, Requirement 3, Requirement 4_
  - _Definition of Done: `npm run test:run` と `tsc --noEmit` が変更範囲で通過する（完了: 2026-04-26）_

- [x] **T6** — 全UI辞書化の対象一覧を合意
  - File: `.spec-workflow/specs/ui-text-consistency/design.md`, `.spec-workflow/specs/ui-text-consistency/requirements.md`
  - 内容: 未辞書化文言の棚卸し結果（優先度、対象ファイル、文言候補、キー案）を仕様に反映し、レビューで合意する
  - 目的: 次フェーズの多言語化に向けて、辞書化対象の境界と実施順を固定する
  - _Leverage: `services/ui/app/**`, 既存 `uiText.ts`, `Confirmed Mapping Table`_
  - _Requirements: Requirement 5_
  - _Definition of Done: `Dictionary Agreement Scope` と `Open Dictionary Candidates` が仕様に反映され、レビュー可能な状態である（完了: 2026-04-26）_

- [x] **T7** — 辞書キー命名規約と未決定事項の合意
  - File: `.spec-workflow/specs/ui-text-consistency/design.md`
  - 内容: `Key Namespace Rules` と `Open Questions` を確定し、辞書実装時の判断基準を固定する
  - 目的: 実装フェーズでキー命名のぶれや再設計を防ぐ
  - _Leverage: `services/ui/app/lib/uiText.ts`_
  - _Requirements: Requirement 5_
  - _Definition of Done: 未決定事項の扱い（イベント種別、ステータス値、技術語翻訳）が合意されている（完了: 2026-04-26）_

---

## 実行メモ

- 着手中は `[ ]` を `[-]`、完了後は `[x]` に更新する。
- 推奨実装順は `T1 -> T2 -> T3 -> T4 -> T5 -> T6 -> T7`。
