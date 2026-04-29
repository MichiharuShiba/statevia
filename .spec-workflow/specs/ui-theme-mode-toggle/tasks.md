# タスク定義書

## 記載ルール

- タスクは小さく独立して完了できる単位で分割する。
- 各タスクには対象ファイル、目的、要件番号、完了条件を記載する。
- 再利用要素がある場合は `_Leverage` に明記する。
- 処理分岐を含む仕様のため、フロー図更新タスクを含める。
- 最終タスクとして `@docs` 更新を必ず含める。

## タスクテンプレート

- [x] 1. テーマ要件と設計の整合を確定する
  - File: `.spec-workflow/specs/ui-theme-mode-toggle/requirements.md`, `.spec-workflow/specs/ui-theme-mode-toggle/design.md`
  - 内容: テーマ値、初期判定、Cookie フォールバック、対象画面範囲を要件/設計で同期する
  - 目的: 実装前に判定ロジックと責務境界を固定する
  - _Leverage: `.spec-workflow/specs/ui-language-mode-toggle/requirements.md`, `.spec-workflow/specs/ui-language-mode-toggle/design.md`_
  - _Requirements: 要件2, 要件3, 要件4_
  - _Definition of Done: 要件と設計で同一ルールが明文化され、レビュー追跡可能な状態になっている_

- [x] 2. テーマ判定ユーティリティを追加する
  - File: `services/ui/app/lib/theme.ts`
  - 内容: `Theme` 型、`isTheme`、`resolveTheme` を実装し不正値フォールバックを定義する
  - 目的: Cookie 値の安全な解釈を共通化する
  - _Leverage: `services/ui/app/lib/i18n.ts`_
  - _Requirements: 要件2, 要件3_
  - _Definition of Done: `light` / `dark` 以外を安全に扱え、他モジュールから再利用できる_

- [x] 3. グローバル配色トークンをテーマ対応にする
  - File: `services/ui/app/globals.css`
  - 内容: ライト/ダーク用 CSS 変数と `color-scheme` 切替を追加する
  - 目的: 全画面で一貫した配色切替を実現する
  - _Leverage: 既存 `--tone-*` 変数_
  - _Requirements: 要件1, 要件4_
  - _Definition of Done: ルートテーマ属性変更のみで主要トークンが切り替わる_

- [x] 4. レイアウトで初期テーマ判定を適用する
  - File: `services/ui/app/layout.tsx`
  - 内容: Cookie 読み取りと初期テーマ反映、未設定時の OS 設定追従処理を実装する
  - 目的: 初回表示時から意図したテーマで描画する
  - _Leverage: 既存ヘッダー/レイアウト構成_
  - _Requirements: 要件2, 要件3_
  - _Definition of Done: Cookie 優先、未設定時 OS 追従の規則で初期描画される_

- [x] 5. 共通ヘッダーにテーマ切替 UI を追加する
  - File: `services/ui/app/components/layout/ThemeToggle.tsx`, `services/ui/app/layout.tsx`
  - 内容: ライト/ダーク切替 UI を実装し、Cookie 更新と再描画処理を組み込む
  - 目的: 利用者が任意タイミングでテーマを変更できるようにする
  - _Leverage: `services/ui/app/components/layout/LanguageToggle.tsx`_
  - _Requirements: 要件1, 要件2_
  - _Definition of Done: ヘッダー操作でテーマが切り替わり、選択状態を視認できる_

- [x] 6. 処理フロー図を作成/更新する
  - File: `.spec-workflow/specs/ui-theme-mode-toggle/requirements.md`, `.spec-workflow/specs/ui-theme-mode-toggle/design.md`
  - 内容: 初期判定、フォールバック、トグル操作後の反映フローを mermaid 図で明示する
  - 目的: テーマ判定分岐を文章だけでなく図でも追跡可能にする
  - _Requirements: 要件2, 要件3_
  - _Definition of Done: 要件・設計のフロー図が実装方針と一致している_

- [x] 7. 主要画面の可読性監査と配色調整を行う
  - File: `services/ui/app/dashboard/DashboardPageClient.tsx`, `services/ui/app/definitions/DefinitionsPageClient.tsx`, `services/ui/app/workflows/WorkflowsPageClient.tsx`
  - 内容: 直書き色クラスを監査し、必要箇所をトークンまたはテーマ対応クラスへ置換する
  - 目的: ライト/ダーク双方の可読性と整合性を担保する
  - _Leverage: `services/ui/app/components/layout/PageShell.tsx`_
  - _Requirements: 要件4_
  - _Definition of Done: 主要画面でテーマ切替後も可読性基準を満たす_

- [x] 8. テストと品質確認を実施する
  - File: `services/ui/tests/**`, `services/ui/app/**` 変更対象
  - 内容: テーマ判定・トグル操作・初期表示判定のテストを追加更新し、型検査を実施する
  - 目的: テーマ機能追加による回帰を防ぐ
  - _Leverage: 既存 UI テスト基盤_
  - _Requirements: 要件1, 要件2, 要件3, 要件4_
  - _Definition of Done: `npm run test:run` と `tsc --noEmit` が変更範囲で通過する_

## 図対応タスク（必要時）

- [x] 9. 図と実装タスクのトレーサビリティを確認する
  - File: `.spec-workflow/specs/ui-theme-mode-toggle/tasks.md`
  - 内容: フロー図の各分岐がどの実装タスクに対応するかを実行時に点検する
  - 目的: 図と実装の乖離を防ぐ
  - _Requirements: 要件2, 要件3_
  - _Definition of Done: 判定分岐とタスクの対応関係がレビューで説明できる_

## 完了時の最終タスク（必須）

- [x] 10. `@docs` の仕様書を更新する
  - File: `docs/*.md`（変更内容に対応する仕様書）
  - 内容: テーマ切替 UI 挙動、保持ルール、フォールバック規則を関連仕様へ反映する
  - 目的: 実装とドキュメントの乖離を防ぎ、後続開発・運用の判断コストを下げる
  - _Leverage: `docs/core-api-interface.md`, `docs/statevia-data-integration-contract.md`_
  - _Requirements: 要件1, 要件2, 要件3, 要件4_
  - _Definition of Done: 変更した仕様が docs に反映され、関連箇所の整合が取れている_
