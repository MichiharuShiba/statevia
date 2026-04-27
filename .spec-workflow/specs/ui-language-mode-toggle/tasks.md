# Tasks: UI言語モード切替（日本語 / 英語）

**spec 名**: `ui-language-mode-toggle`  
**要件**: `requirements.md`  
**設計**: `design.md`

---

- [x] **T1** — 言語仕様と辞書キーの確定
  - File: `.spec-workflow/specs/ui-language-mode-toggle/requirements.md`, `.spec-workflow/specs/ui-language-mode-toggle/design.md`
  - 内容: `ja` / `en` の対象範囲、既定値、フォールバック、辞書キー整合ルールを確定する
  - 目的: 実装着手前に言語仕様の境界を固定する
  - _Leverage: `services/ui/app/lib/uiText.ts`_
  - _Requirements: Requirement 2, Requirement 3_
  - _Definition of Done: 要件と設計に同一ルールが明文化され、レビューで追跡できる（完了: 2026-04-27）_

- [x] **T2** — i18n 基盤の追加（Locale / Dictionary）
  - File: `services/ui/app/lib/i18n.ts`, `services/ui/app/lib/uiText.ja.ts`, `services/ui/app/lib/uiText.en.ts`, `services/ui/app/lib/uiText.ts`
  - 内容: ロケール型、フォールバック関数、`ja` / `en` 辞書、辞書取得関数を実装する
  - 目的: 言語切替の土台を型安全に整える
  - _Leverage: 既存 `UiText` 型定義_
  - _Requirements: Requirement 2, Requirement 3_
  - _Definition of Done: 型検査で辞書キー不足を検知でき、`getUiText` が利用可能になる（完了: 2026-04-27）_

- [x] **T3** — Provider / Hook による文言参照統一
  - File: `services/ui/app/lib/uiTextContext.tsx`, `services/ui/app/layout.tsx`
  - 内容: `UiTextProvider`, `useUiText`, `useLocale` を実装し、レイアウトで適用する
  - 目的: 画面側の文言取得口を一元化する
  - _Leverage: `services/ui/app/layout.tsx`_
  - _Requirements: Requirement 3_
  - _Definition of Done: 主要画面で `uiText` を Provider 経由で参照できる（完了: 2026-04-27）_

- [x] **T4** — 共通ヘッダーに言語切替 UI を追加
  - File: `services/ui/app/components/layout/LanguageToggle.tsx`, `services/ui/app/layout.tsx`
  - 内容: 言語トグル UI を実装し、Cookie 更新と再描画処理を追加する
  - 目的: 利用者が任意タイミングで言語を変更できるようにする
  - _Leverage: 既存ヘッダー領域とナビゲーション_
  - _Requirements: Requirement 1, Requirement 2_
  - _Definition of Done: ヘッダー操作で言語が切り替わり、選択状態が視認できる（完了: 2026-04-27）_

- [x] **T5** — 主要画面の文言参照を置換
  - File: `services/ui/app/dashboard/DashboardPageClient.tsx`, `services/ui/app/definitions/DefinitionsPageClient.tsx`, `services/ui/app/workflows/WorkflowsPageClient.tsx` ほか `uiText` 直接参照ファイル
  - 内容: `import { uiText }` 直接参照を `useUiText()` に段階置換する
  - 目的: 画面全体で言語切替を有効化する
  - _Leverage: 既存 `uiText` 利用箇所_
  - _Requirements: Requirement 1, Requirement 3_
  - _Definition of Done: 対象画面で言語混在なく切替表示される（完了: 2026-04-27）_

- [x] **T6** — 日時ロケール対応の適用
  - File: `services/ui/app/dashboard/DashboardPageClient.tsx`, `services/ui/app/definitions/DefinitionsPageClient.tsx`, `services/ui/app/workflows/WorkflowsPageClient.tsx` ほか日時表示箇所
  - 内容: `toLocaleString("ja-JP", ...)` 固定をロケール依存化し、共通ユーティリティで統一する
  - 目的: 言語切替時の表示一貫性を担保する
  - _Leverage: `services/ui/app/lib/i18n.ts`_
  - _Requirements: Requirement 4_
  - _Definition of Done: 日本語/英語それぞれで自然な日時形式になる（完了: 2026-04-27）_

- [x] **T7** — テストと品質確認
  - File: `services/ui/tests/**`, `services/ui/app/**` 変更対象
  - 内容: 単体・結合テストを追加更新し、`npm run test:run` と `tsc --noEmit` を確認する
  - 目的: 言語切替追加による回帰を防ぐ
  - _Leverage: 既存 UI テスト基盤_
  - _Requirements: Requirement 1, Requirement 2, Requirement 3, Requirement 4_
  - _Definition of Done: 変更範囲のテストと型検査が通過する（完了: 2026-04-27）_

---

## 実行メモ

- 着手時は `[ ]` を `[-]`、完了後は `[x]` に更新する。
- 推奨実装順は `T1 -> T2 -> T3 -> T4 -> T5 -> T6 -> T7`。
- 辞書キー追加時は `ja` / `en` 両方を同時更新し、型エラーを未解消のまま残さない。
