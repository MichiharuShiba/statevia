# Tasks: UIハイブリッド刷新（一覧・詳細ハブ + 専用画面分離）

**spec 名**: `ui-hybrid-navigation-refresh`  
**要件**: `requirements.md`  
**設計**: `design.md`

---

- [x] **T1** — ルートリダイレクトの導入
  - File: `services/ui/app/page.tsx`
  - 内容: `app/page.tsx` を `redirect("/dashboard")` のみを担うルートに変更する
  - 目的: TOP導線を `/dashboard` に統一し、入口を一本化する
  - _Leverage: `next/navigation` の `redirect`_
  - _Requirements: Requirement 2_
  - _Definition of Done: `/` 直アクセスで `/dashboard` へ遷移し、旧Execution UI直描画が行われない_

- [x] **T2** — TOPダッシュボード画面の新設
  - File: `services/ui/app/dashboard/page.tsx`, `services/ui/app/dashboard/DashboardPageClient.tsx`, `services/ui/app/lib/types.ts`
  - 内容: 直近WorkflowDetail 10件を取得して表示するダッシュボードを実装し、0件時の空状態を追加する
  - 目的: 日常運用の再開を最短化する
  - _Leverage: `services/ui/app/lib/api.ts`, `services/ui/app/lib/statusStyle.ts`, `services/ui/app/components/execution/TenantMissingBanner.tsx`, `services/ui/app/components/Toast.tsx`_
  - _Requirements: Requirement 2_
  - _Definition of Done: 10件表示/空状態/詳細遷移が動作する_

- [x] **T3** — Definition一覧ページの追加
  - File: `services/ui/app/definitions/page.tsx`, `services/ui/app/definitions/DefinitionsPageClient.tsx`, `services/ui/app/lib/types.ts`
  - 内容: Definition一覧（検索・ページング）と `DefinitionDetail` への遷移導線を追加する
  - 目的: 定義起点の導線を確立する
  - _Leverage: `services/ui/app/lib/api.ts`, `services/ui/app/playground/page.tsx` の定義取得処理_
  - _Requirements: Requirement 1, Requirement 3_
  - _Definition of Done: Definition一覧から対象詳細へ遷移できる_

- [x] **T4** — Definition詳細ページの追加
  - File: `services/ui/app/definitions/[definitionId]/page.tsx`
  - 内容: Definitionメタ情報、関連Workflow導線、編集導線、実行開始導線を実装する
  - 目的: Definitionをハブとして運用導線を接続する
  - _Leverage: `services/ui/app/lib/types.ts`, `services/ui/app/lib/errors.ts`_
  - _Requirements: Requirement 1, Requirement 4_
  - _Definition of Done: Workflow一覧・Editor・Run開始へ遷移可能_

- [x] **T5** — Workflow一覧ページの追加
  - File: `services/ui/app/workflows/page.tsx`, `services/ui/app/lib/api.ts`
  - 内容: status/name/filter と limit/offset を扱うWorkflow一覧を実装し、Definition文脈フィルタを受け取れるようにする
  - 目的: ID手入力依存を解消して一覧起点参照を可能にする
  - _Leverage: `services/ui/app/features/execution/useExecution.ts`_
  - _Requirements: Requirement 1, Requirement 3_
  - _Definition of Done: 一覧から `WorkflowDetail` へURL遷移でき、再読込で状態が復元される_

- [x] **T6** — Workflow詳細ページのURL主導化
  - File: `services/ui/app/workflows/[workflowId]/page.tsx`, `services/ui/app/components/execution/ExecutionHeader.tsx`
  - 内容: 手入力ID中心UIを廃止し、URLパラメータで対象実行をロードする詳細ページへ再構成する
  - 目的: 共有可能な詳細参照を実現する
  - _Leverage: `services/ui/app/components/execution/ExecutionDashboard.tsx`, `services/ui/app/features/execution/useExecution.ts`_
  - _Requirements: Requirement 1, Requirement 3_
  - _Definition of Done: URLのみで詳細表示が成立し、存在しないIDで適切な案内が表示される_

- [x] **T7** — Definition起点のRun開始ページを追加
  - File: `services/ui/app/definitions/[definitionId]/run/page.tsx`
  - 内容: Definition選択から新規Workflow開始（`POST /v1/workflows`）を実行し、開始後に `WorkflowRunPage` へ遷移する
  - 目的: 定義起点実行フローを固定する
  - _Leverage: `services/ui/app/playground/page.tsx` の start 処理_
  - _Requirements: Requirement 1, Requirement 4_
  - _Definition of Done: DefinitionList起点で新規開始からrun画面表示まで完結する_

- [x] **T8** — WorkflowRun専用ページの分離
  - File: `services/ui/app/workflows/[workflowId]/run/page.tsx`, `services/ui/app/features/nodes/useNodeCommands.ts`
  - 内容: run画面に Cancel/Resume/Event を集約し、実行系操作を詳細画面から分離する
  - 目的: 画面責務を明確化し誤操作を減らす
  - _Leverage: `services/ui/app/components/execution/ExecutionTimeline.tsx`, `services/ui/app/features/execution/useExecutionEvents.ts`_
  - _Requirements: Requirement 4_
  - _Definition of Done: run画面だけで主要操作が完結する_

- [ ] **T9** — WorkflowGraph専用ページの分離
  - File: `services/ui/app/workflows/[workflowId]/graph/page.tsx`, `services/ui/app/features/graph/useGraphData.ts`
  - 内容: グラフ表示を専用画面化し、Run/Detailからの遷移導線と戻り導線を実装する
  - 目的: 可視化体験を独立させ、運用時の探索性を高める
  - _Leverage: `services/ui/app/components/nodes/NodeGraphView.tsx`, `services/ui/app/components/nodes/NodeDetail.tsx`_
  - _Requirements: Requirement 1, Requirement 4_
  - _Definition of Done: run/detail から graph へ往復可能で状態表示が維持される_

- [ ] **T10** — DefinitionEditor専用ページの分離
  - File: `services/ui/app/definitions/[definitionId]/edit/page.tsx`, `services/ui/app/playground/defaultYaml.ts`
  - 内容: Playground簡易編集を分離し、DefinitionEditorで検証・保存・エラー表示の導線を整備する
  - 目的: 定義編集責務を専用画面に集約する
  - _Leverage: `services/ui/app/playground/page.tsx`_
  - _Requirements: Requirement 1, Requirement 4_
  - _Definition of Done: DefinitionListから編集に遷移し、保存成否が視覚的に判別できる_

- [ ] **T11** — 互換導線の整備
  - File: `services/ui/app/playground/page.tsx`, `services/ui/app/playground/run/[displayId]/page.tsx`
  - 内容: 旧導線に新URLへの案内を実装し、移行期間の利用者迷子を防ぐ
  - 目的: 無停止移行を実現する
  - _Leverage: 既存 Playground 画面_
  - _Requirements: Requirement 5_
  - _Definition of Done: 旧導線から新導線への遷移リンクがあり、既存機能を阻害しない_

- [ ] **T12** — テストと品質確認
  - File: `services/ui/tests/**`（必要に応じて追加）, `services/ui/app/**` 変更対象
  - 内容: 遷移導線、URL復元、実行操作、グラフ遷移、`/` リダイレクトを検証するテストを追加・更新し、型検査を実施する
  - 目的: UI刷新の回帰を防ぐ
  - _Leverage: 既存 `tests/lib/workflowView.test.ts`, `tests/features/execution/useExecution.test.ts`_
  - _Requirements: Requirement 1, Requirement 2, Requirement 3, Requirement 4, Requirement 5_
  - _Definition of Done: `npm run test:run` と `tsc --noEmit` が変更範囲で通過する_

---

## 実行メモ

- 着手中は `[ ]` を `[-]`、完了後は `[x]` に更新する。
- 推奨実装順は `T1 -> T2 -> T3 -> T4 -> T5 -> T6 -> T7 -> T8 -> T9 -> T10 -> T11 -> T12`。
- `docs/` への反映は実装完了後に同期タスクとして実施する。
