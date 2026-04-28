# タスク定義書

## 記載ルール

- タスクは小さく独立して完了できる単位で分割する。
- 各タスクに対象ファイル、目的、要件番号、完了条件を記載する。
- 既存資産を使う場合は `_Leverage` を必ず記載する。
- 処理フロー図（requirements/design）と対応する実装タスクを含める。

## タスク一覧

- [x] 1. Definition一覧に新規作成導線を追加する
  - File: `services/ui/app/definitions/DefinitionsPageClient.tsx`, `services/ui/app/lib/uiText.ts`, `services/ui/app/lib/uiText.en.ts`
  - 内容: 一覧画面に `/definitions/new` へ遷移する導線と文言を追加する
  - 目的: 詳細画面を経由せず定義作成を開始できるようにする
  - _Leverage: `DefinitionsPageClient` 既存検索フォーム、`useI18n` 文言辞書_
  - _Requirements: 要件1_
  - _Definition of Done: 一覧画面から新規作成画面へ遷移できる_

- [x] 2. 新規作成ルートを追加し既存エディタを再利用する
  - File: `services/ui/app/definitions/new/page.tsx`, `services/ui/app/definitions/[definitionId]/edit/DefinitionEditorPageClient.tsx`
  - 内容: `definitionId` 非依存の新規作成モードを追加する
  - 目的: 既存エディタ資産を活かして最小差分で新規作成を実現する
  - _Leverage: `DefinitionEditorPageClient` の既存保存処理とテンプレート初期値_
  - _Requirements: 要件1_
  - _Definition of Done: `/definitions/new` で編集開始と保存操作が可能_

- [x] 3. CodeMirror導入と補完機能を実装する
  - File: `services/ui/app/definitions/[definitionId]/edit/DefinitionEditorPageClient.tsx`, `services/ui/app/definitions/new/*`, 必要な `services/ui/app/lib/*`
  - 内容: YAML入力欄を CodeMirror 化し、スキーマ由来の補完候補を表示する
  - 目的: 入力速度向上と記述ミス削減を図る
  - _Leverage: 既存YAML初期値、既存エディタ画面レイアウト_
  - _Requirements: 要件2_
  - _Definition of Done: CodeMirror で補完候補が提示される_

- [x] 4. UI側Lintと保存ガードを実装する
  - File: `services/ui/app/definitions/[definitionId]/edit/DefinitionEditorPageClient.tsx`, `services/ui/app/definitions/new/*`
  - 内容: エラー下線、ヒント表示、Lintエラー時の保存無効化を実装する
  - 目的: 不正定義の誤保存を防ぐ
  - _Leverage: CodeMirror lint extension_
  - _Requirements: 要件3_
  - _Definition of Done: エラー状態では保存不可となり、診断が可視化される_

- [x] 5. API最終検証と422診断契約を整備する
  - File: `api/Statevia.Core.Api/Controllers/DefinitionsController.cs`, `api/Statevia.Core.Api/Services/DefinitionService.cs`, 関連 contract
  - 内容: 保存時の厳格検証と 422 構造化診断レスポンスを整備する
  - 目的: UI判定を通過した不正入力を API で確実に拒否する
  - _Leverage: `IDefinitionCompilerService.ValidateAndCompile`_
  - _Requirements: 要件3_
  - _Definition of Done: 不正定義保存時に 422 診断が返り保存されない_

- [x] 6. Nodesスキーマ配布APIを追加する
  - File: `api/Statevia.Core.Api/Controllers/DefinitionsController.cs` または専用 controller, `api/Statevia.Core.Api/Application/Definition/*`
  - 内容: `GET /v1/definitions/schema/nodes` を追加し、`schemaVersion`、`nodesVersion`、`schema` を返却する
  - 目的: UI補完/Lintの源泉を API に集約する
  - _Leverage: `NodesWorkflowDefinitionLoader` の既存仕様_
  - _Requirements: 要件4_
  - _Definition of Done: UIが配布スキーマを取得して利用できる_

- [x] 7. 将来拡張に向けたスキーマ生成移行余地を設計する
  - File: `api/Statevia.Core.Api/Application/Definition/*`, `api/Statevia.Core.Api/Abstractions/*`, `.spec-workflow/specs/definition-editor-intelligence/design.md`
  - 内容: DTO起点スキーマ生成へ移行可能な責務境界を定義し設計へ反映する
  - 目的: `nodes` 拡張時の手修正コストを抑える
  - _Leverage: 既存 loader/validator 実装_
  - _Requirements: 要件5_
  - _Definition of Done: 移行方針が設計またはコード境界として明示される_

- [x] 8. テストと品質確認を実施する
  - File: `services/ui/tests/**`, `api/Statevia.Core.Api.Tests/**`, `.spec-workflow/specs/definition-editor-intelligence/*.md`
  - 内容: UI/APIテスト追加、型チェック・テスト実行、仕様書同期を行う
  - 目的: 導線、補完、Lint、スキーマ配布の回帰を防ぐ
  - _Leverage: 既存 UI/API テスト基盤_
  - _Requirements: 要件1, 要件2, 要件3, 要件4, 要件5_
  - _Definition of Done: 変更範囲のテストと静的検査が通過する_

## 図対応タスク

- [x] 9. 処理フロー図を更新し実装内容と同期する
  - File: `.spec-workflow/specs/definition-editor-intelligence/requirements.md`, `.spec-workflow/specs/definition-editor-intelligence/design.md`
  - 内容: 一覧導線、スキーマ取得、二段階検証、エラー表示フローを mermaid 図で同期する
  - 目的: 仕様と実装の追跡性を高める
  - _Requirements: 要件1, 要件2, 要件3, 要件4_
  - _Definition of Done: 図とタスク実装の対応がレビューで確認できる_
