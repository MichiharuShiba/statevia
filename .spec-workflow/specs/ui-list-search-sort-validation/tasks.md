# タスク定義書

## 記載ルール

- タスクは **小さく独立して完了できる単位** に分割する
- 各タスクに **対象ファイル / 目的 / 要件番号 / 完了条件** を必ず記載する
- 既存資産を使う場合は `_Leverage` に明記する
- 複雑な仕様では、タスク本文だけでなく**処理フロー図に対応する実装タスク**を含める
- 最終タスクとして **`@docs` の仕様更新** を必ず追加する（API IF / 振る舞い / エラー契約の変更を反映）

## タスク一覧

- [ ] 1. 一覧クエリモデルを拡張する
  - File: `services/ui/app/lib/api.ts`
  - 内容: `sortBy` / `sortOrder` を含む一覧クエリ型とパス生成処理を追加し、空値除外と正規化を統一する
  - 目的: 検索・ソート機能を型安全に実装する基盤を整える
  - _Leverage: 既存 `buildWorkflowsListPath`, `WorkflowsListQuery`_
  - _Requirements: 1, 2_
  - _Definition of Done: 無効値を安全に除外/フォールバックし、一覧APIパスを一貫生成できる_

- [ ] 2. Definitions 一覧に詳細フィルタを追加する
  - File: `services/ui/app/definitions/DefinitionsPageClient.tsx`
  - 内容: 既存の名前検索を拡張し、複数条件の入力・条件クリア・ページング連携を実装する
  - 目的: Definitions 画面で探索効率を向上させる
  - _Leverage: 既存ページングUI, 検索フォーム_
  - _Requirements: 1_
  - _Definition of Done: 複数条件検索とページングが矛盾なく動作する_

- [ ] 3. Workflows 一覧にソートUIを追加する
  - File: `services/ui/app/workflows/WorkflowsPageClient.tsx`
  - 内容: ソート項目と昇順/降順切替UIを追加し、URL同期と再取得を行う
  - 目的: Workflows 画面で時系列・属性別の確認を容易にする
  - _Leverage: 既存 `readListQuery`, `goTo`, `ListPagination`_
  - _Requirements: 2_
  - _Definition of Done: ソート操作時にURLと一覧表示が同期し、無効値は既定化される_

- [ ] 4. Definition 編集フォームの入力検証を強化する
  - File: `services/ui/app/definitions/DefinitionEditorPageClient.tsx`
  - 内容: 名前フィールドの文字種・長さ・危険文字チェックを追加し、エラー表示を整理する
  - 目的: 不正入力の早期検知と保存失敗の削減を実現する
  - _Leverage: 既存 `extractApiValidationDetails`, `extractApiDiagnosticMessages`_
  - _Requirements: 3_
  - _Definition of Done: 保存前検証で不正入力をブロックし、修正箇所が明確に表示される_

- [ ] 5. API 422エラー表示の共通化を行う
  - File: `services/ui/app/definitions/DefinitionEditorPageClient.tsx`, `services/ui/app/lib/errors.ts`
  - 内容: 422 `details` の表示ロジックを共通化し、フィールド別と全体エラーの表示方針を統一する
  - 目的: 画面ごとのエラー体験差を解消する
  - _Leverage: 既存トースト変換ロジック_
  - _Requirements: 3_
  - _Definition of Done: 422詳細が一貫形式で表示される_

- [ ] 6. 定義実行開始フォームの入力検証を追加する
  - File: `services/ui/app/definitions/[definitionId]/run/page.tsx`
  - 内容: `workflowInput` のサイズ上限とJSON構文チェックの表示を明確化し、不正時の送信抑止を実装する
  - 目的: 実行開始時の入力不正による失敗を減らす
  - _Leverage: 既存 JSON.parse チェック, Toast 表示_
  - _Requirements: 3_
  - _Definition of Done: JSON不正・過大入力時に開始処理が走らず、修正可能なエラー表示になる_

- [ ] 7. 実行画面イベント送信フォームの入力検証を追加する
  - File: `services/ui/app/components/execution/ExecutionDashboard.tsx`, `services/ui/app/features/execution/useExecution.ts`
  - 内容: `eventName` の文字種/長さ制約と送信前バリデーションを実装し、エラー表示を追加する
  - 目的: イベント送信の誤入力を防ぎ、運用時の追跡性を高める
  - _Leverage: 既存 `eventName.trim()` と送信ボタンの disabled 制御_
  - _Requirements: 3_
  - _Definition of Done: 許可外形式・過長イベント名は送信不可で、理由が画面上で確認できる_

- [ ] 8. UI文言を追加・統一する
  - File: `services/ui/app/lib/uiText.ts`
  - 内容: 検索詳細条件、ソート関連、入力検証エラー文言を追加し、画面間で用語を揃える
  - 目的: 操作理解性と一貫性を向上させる
  - _Leverage: 既存 `uiText` 構造_
  - _Requirements: 1, 2, 3_
  - _Definition of Done: 新規UI要素に必要な文言が不足なく提供される_

- [ ] 9. 処理フロー図と実装追跡の整合を確認する
  - File: `.spec-workflow/specs/ui-list-search-sort-validation/design.md`, `.spec-workflow/specs/ui-list-search-sort-validation/requirements.md`
  - 内容: 仕様内mermaid図と実装タスクの対応関係をレビューし、不整合を修正する
  - 目的: 仕様と実装の追跡可能性を確保する
  - _Leverage: 本仕様の既存フロー図_
  - _Requirements: 1, 2, 3_
  - _Definition of Done: フロー図・要件・タスクが相互参照で矛盾しない_

- [ ] 10. `@docs` の仕様書を更新する
  - File: `docs/core-api-interface.md`, `docs/statevia-data-integration-contract.md`, `docs/ui-visual-spec.md`
  - 内容: 検索/ソートパラメータ、UI挙動、入力検証・422エラー表示方針の変更を反映する
  - 目的: 実装と仕様書の乖離を防ぐ
  - _Leverage: 既存 docs のAPI契約とUI仕様_
  - _Requirements: 1, 2, 3_
  - _Definition of Done: 実装変更点が docs に反映され、関連章の整合が取れている_
