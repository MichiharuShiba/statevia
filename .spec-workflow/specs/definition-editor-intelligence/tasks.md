# Tasks: Definition作成導線改修とエディタインテリジェンス

**spec 名**: `definition-editor-intelligence`  
**要件**: `requirements.md`  
**設計**: `design.md`

---

- [ ] **T1** — Definition一覧に新規作成導線を追加
  - File: `services/ui/app/definitions/DefinitionsPageClient.tsx`, `services/ui/app/lib/uiText.ts`, `services/ui/app/lib/uiText.en.ts`
  - 内容: 一覧画面から `/definitions/new` へ遷移できるボタンと文言を追加する
  - 目的: 詳細画面経由なしで定義作成を開始できるようにする
  - _Leverage: 既存の検索フォームと `useI18n` 文言構造_
  - _Requirements: Requirement 1_
  - _Definition of Done: 一覧画面に新規作成導線が表示され、遷移できる_

- [ ] **T2** — 新規作成ルートを追加し既存エディタを再利用
  - File: `services/ui/app/definitions/new/page.tsx`, `services/ui/app/definitions/new/DefinitionEditorNewPageClient.tsx`（必要時）, 既存 editor client
  - 内容: 新規作成モードを追加し、`definitionId` 非依存でエディタを表示できるようにする
  - 目的: 既存エディタ資産を活かしつつ新規作成導線を成立させる
  - _Leverage: `services/ui/app/definitions/[definitionId]/edit/DefinitionEditorPageClient.tsx`_
  - _Requirements: Requirement 1_
  - _Definition of Done: `/definitions/new` で編集・保存操作が可能_

- [ ] **T3** — CodeMirror導入と補完機能実装
  - File: `services/ui/app/definitions/[definitionId]/edit/DefinitionEditorPageClient.tsx`, `services/ui/app/definitions/new/*`, 必要な `services/ui/app/lib/*`
  - 内容: YAML編集欄を CodeMirror に置換し、スキーマ由来の補完候補を表示する
  - 目的: 入力速度向上と入力ミスの削減
  - _Leverage: 既存の YAML 初期値テンプレートと保存処理_
  - _Requirements: Requirement 2_
  - _Definition of Done: YAML欄が CodeMirror 化され、補完候補が表示される_

- [ ] **T4** — UI側Lintと保存ガードの実装
  - File: `services/ui/app/definitions/[definitionId]/edit/DefinitionEditorPageClient.tsx`, `services/ui/app/definitions/new/*`
  - 内容: エラー箇所下線・ヒント表示、エラー時保存無効化を実装する
  - 目的: 不正定義の早期検知と誤保存防止
  - _Leverage: CodeMirror lint extension_
  - _Requirements: Requirement 3_
  - _Definition of Done: Lintエラー時に保存ボタンが無効化され、下線とヒントが表示される_

- [ ] **T5** — API側最終検証と422診断整備
  - File: `api/Statevia.Core.Api/Controllers/DefinitionsController.cs`, `api/Statevia.Core.Api/Services/DefinitionService.cs`, 必要な contract
  - 内容: 保存時の厳格検証を整備し、422で構造化診断を返却する
  - 目的: UI判定をすり抜けた不正定義を確実に拒否する
  - _Leverage: `IDefinitionCompilerService.ValidateAndCompile`_
  - _Requirements: Requirement 3_
  - _Definition of Done: 不正定義保存で422診断が返り、保存されない_

- [ ] **T6** — Nodesスキーマ配布APIを追加
  - File: `api/Statevia.Core.Api/Controllers/DefinitionsController.cs`（または専用 Controller）, `api/Statevia.Core.Api/Application/Definition/*`
  - 内容: `GET /v1/definitions/schema/nodes`（仮）を追加し、`schemaVersion` / `nodesVersion` / `schema` を返す
  - 目的: UIの補完/Lint源泉をAPIへ集約する
  - _Leverage: `NodesWorkflowDefinitionLoader` と既存定義ルール_
  - _Requirements: Requirement 4_
  - _Definition of Done: UIがスキーマAPIを呼び出して補完/Lintに利用できる_

- [ ] **T7** — 将来拡張性のための移行余地を実装に残す
  - File: `api/Statevia.Core.Api/Application/Definition/*`, `api/Statevia.Core.Api/Abstractions/*`, 関連ドキュメント
  - 内容: DTO起点スキーマ生成へ移行できる境界（interface/責務分離）を定義する
  - 目的: nodes拡張時のスキーマ手修正コストを下げる
  - _Leverage: 現行の loader/validator 実装_
  - _Requirements: Requirement 5_
  - _Definition of Done: 将来DTO生成へ差し替え可能な設計意図がコードまたはドキュメントで明示される_

- [ ] **T8** — テストと品質確認
  - File: `services/ui/tests/**`, `api/Statevia.Core.Api.Tests/**`, `.spec-workflow/specs/definition-editor-intelligence/*.md`
  - 内容: UI/APIテスト追加、型チェック・テスト実行、必要な仕様追記を行う
  - 目的: 導線・補完・Lint・スキーマ配布の回帰を防ぐ
  - _Leverage: 既存の UI / API テスト基盤_
  - _Requirements: Requirement 1, Requirement 2, Requirement 3, Requirement 4, Requirement 5_
  - _Definition of Done: 変更範囲のテストと静的検査が通過する_

---

## 実行メモ

- 着手中は `[ ]` を `[-]`、完了後は `[x]` に更新する。
- 推奨順序は `T1 -> T2 -> T3 -> T4 -> T5 -> T6 -> T7 -> T8`。
- APIスキーマ仕様を変更した場合は、UIフォールバック定義とテストケースを同時更新する。
