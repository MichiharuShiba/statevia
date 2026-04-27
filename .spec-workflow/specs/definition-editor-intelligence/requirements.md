# Requirements: Definition作成導線改修とエディタインテリジェンス

## Introduction

本仕様は、Definition の新規作成導線を改善し、定義エディタに入力補助とLint機能を導入する。
あわせて、`nodes` 形式スキーマを API から配布し、UI 側の仕様追従コストを下げる。

## Alignment with Product Vision

- 定義駆動開発の体験を強化し、作成から実行までのリードタイムを短縮する。
- UI と API の責務を分離し、検証ロジックの正を API 側に寄せる。
- 将来の `nodes` 拡張時に、UIのハードコード修正を最小化する。

## Requirements

### Requirement 1 — Definition一覧から新規作成へ直接遷移できる

**User Story:** As a **運用者**, I want **Definition一覧から新規作成画面に遷移したい** so that **詳細画面を経由せずに作成を開始できる**.

#### Acceptance Criteria — Requirement 1

1. WHEN 利用者が Definition 一覧を開く THEN システム SHALL 新規作成導線を表示する。
2. WHEN 利用者が新規作成導線を押下する THEN システム SHALL 新規定義作成画面へ遷移する。
3. WHEN 新規作成画面を表示する THEN システム SHALL 既存定義ID依存の読込を必須としない。

### Requirement 2 — 定義エディタに入力補助を提供する

**User Story:** As a **定義作成者**, I want **YAML記述時に補完候補を利用したい** so that **入力ミスを減らして作成速度を上げられる**.

#### Acceptance Criteria — Requirement 2

1. WHEN 利用者が YAML を編集する THEN システム SHALL CodeMirror ベースのエディタを提供する。
2. WHEN カーソル位置や入力文脈に応じて補完候補が取得可能な状態である THEN システム SHALL 候補を提示する。
3. WHEN APIスキーマ取得が失敗する THEN システム SHALL ローカルの最小候補セットへフォールバックする。

### Requirement 3 — インラインLintと保存制御（厳格）を提供する

**User Story:** As a **定義作成者**, I want **記述ミスを下線とヒントで確認したい** so that **保存前に不正定義を修正できる**.

#### Acceptance Criteria — Requirement 3

1. WHEN YAML に構文または形式エラーがある THEN システム SHALL 該当箇所を下線表示し、ヒントを表示する。
2. WHEN UI側Lintエラーが存在する THEN システム SHALL 保存操作を無効化する。
3. WHEN API 側検証で不正と判定される THEN システム SHALL 422 を返し、UI は診断をエディタ上に再表示する。
4. WHEN UI側Lintが通過しているが API 検証で失敗する THEN システム SHALL API側判定を正として保存を拒否する。

### Requirement 4 — APIからnodesスキーマを配布する

**User Story:** As an **UI実装者**, I want **nodes 形式スキーマを API から取得したい** so that **仕様変更に追従しやすくしたい**.

#### Acceptance Criteria — Requirement 4

1. WHEN クライアントがスキーマ取得 API を呼ぶ THEN システム SHALL `nodes` 形式のスキーマ情報を返す。
2. WHEN スキーマを返す THEN システム SHALL `schemaVersion` と `nodesVersion` を含める。
3. WHEN nodes 仕様が拡張される THEN システム SHALL バージョン互換性を維持できる配布形式を提供する。

### Requirement 5 — 将来拡張時の保守コストを抑える

**User Story:** As a **開発者**, I want **スキーマ定義の更新コストを下げたい** so that **nodes 拡張時の変更箇所を局所化できる**.

#### Acceptance Criteria — Requirement 5

1. WHEN 当面運用するスキーマを整備する THEN システム SHALL API配布スキーマを単一参照源として扱う。
2. WHEN 中期的な改善を計画する THEN システム SHALL DTO 起点のスキーマ生成方式へ移行可能な設計を保持する。
3. WHEN ランタイム性能を評価する THEN システム SHALL スキーマ生成をリクエスト毎ではなく起動時またはビルド時に行う方針を採用する。

## Non-Functional Requirements

### Clarity

- エディタの補完源泉（API配布スキーマ、フォールバック候補）を明示すること。
- UI判定とAPI判定の優先順位（API正）を明示すること。

### Reliability

- 不正定義は UI・API の双方で保存不可とすること。
- スキーマ取得失敗時も機能停止せず、ローカルフォールバックで編集継続できること。

### Performance

- スキーマ取得は過剰再フェッチを避け、キャッシュ戦略を導入すること。
- 補完/Lintにより編集体験が著しく劣化しないこと。

## Out of Scope

- Monaco Editor への置換。
- `nodes` 以外の新定義フォーマット追加。
- 認証ユーザープロファイルへのエディタ設定保存。

## References

- `.spec-workflow/specs/ui-language-mode-toggle/requirements.md`
- `services/ui/app/definitions/DefinitionsPageClient.tsx`
- `services/ui/app/definitions/[definitionId]/edit/DefinitionEditorPageClient.tsx`
- `api/Statevia.Core.Api/Application/Definition/NodesWorkflowDefinitionLoader.cs`
