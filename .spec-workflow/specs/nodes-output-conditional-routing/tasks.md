# Tasks: nodes→states 変換残課題と output 条件遷移

**spec 名**: `nodes-output-conditional-routing`  
**要件**: `requirements.md`  
**設計**: `design.md`

---

- [x] **T1** — 定義モデルとローダー拡張の基盤追加
  - File: `engine/Statevia.Core.Engine/Definition/WorkflowDefinition.cs`, `engine/Statevia.Core.Engine/Definition/StateWorkflowDefinitionLoader.cs`
  - 内容: `on.<Fact>.cases/default` を表現できる定義モデルを追加し、states 形式の YAML ローダーが `default` ショートハンド、`order`、`when.path/op/value`、`in`、`between` を読み込めるようにする
  - 目的: 条件遷移を既存 `on: <Fact>` 互換のまま保持できるようにする
  - _Leverage: `engine/Statevia.Core.Engine/Definition/WorkflowDefinition.cs`, `engine/Statevia.Core.Engine/Definition/StateWorkflowDefinitionLoader.cs`_
  - _Requirements: Requirement 1, Requirement 2, Requirement 3_
  - _Definition of Done: states 形式の定義モデルとローダーで `cases/default` と `default: <State>` を表現できる_

- [x] **T2** — nodes 形式の互換入力正規化を実装
  - File: `api/Statevia.Core.Api/Application/Definition/NodesWorkflowDefinitionLoader.cs`
  - 内容: `nodes.next` と単一無条件 `nodes.edges.to.id` を同一セマンティクスとして扱い、条件付き edge を `on.Completed.cases[]`、無条件 edge を `on.Completed.default` に変換する
  - 目的: 既存 `nodes.next` を維持しつつ、新しい条件遷移表現へ段階移行できるようにする
  - _Leverage: `api/Statevia.Core.Api/Application/Definition/NodesWorkflowDefinitionLoader.cs`_
  - _Requirements: Requirement 1, Requirement 4_
  - _Definition of Done: `nodes.next` と `nodes.edges` の両入力を受理し、期待どおりの states 定義へ正規化される_

- [x] **T3** — 定義バリデーションを追加
  - File: `engine/Statevia.Core.Engine/Definition/Validation/Level1Validator.cs`, `api/Statevia.Core.Api/Application/Definition/NodesWorkflowDefinitionLoader.cs`
  - 内容: `next/fork/end` と `cases/default` の混在禁止、`default` 必須、`default` オブジェクトの単一遷移制約、`when.path` の簡易 JSONPath 制約、`in` と `between` の value 形状制約、`nodes.next` と `nodes.edges.to.id` 併記時の一致確認、`nodes` 形式の単一 `type: end` 制約、`states` 形式の終端必須、空遷移禁止、`end: true` と `next/fork` の併記禁止を実装する
  - 目的: 実行前に解釈不能な定義を 422 で拒否する
  - _Leverage: `engine/Statevia.Core.Engine/Definition/Validation/Level1Validator.cs`, `api/Statevia.Core.Api/Application/Definition/NodesWorkflowDefinitionLoader.cs`_
  - _Requirements: Requirement 2, Requirement 3, Requirement 4_
  - _Definition of Done: 不正定義が登録時に明示的エラーとなり、許可ケースのみ通過する_

- [x] **T4** — コンパイル済み遷移モデルへ条件情報を反映
  - File: `engine/Statevia.Core.Engine/Definition/DefinitionCompiler.cs`, `engine/Statevia.Core.Engine/Abstractions/CompiledWorkflowDefinition.cs`
  - 内容: `cases/default` を実行時に参照できるコンパイル済み表現へ落とし込み、`order` と記載順を保持し、`states` 形式の複数 `end: true` をそのまま終端遷移として保持する
  - 目的: エンジンが deterministic に条件遷移を評価できる状態を作る
  - _Leverage: `engine/Statevia.Core.Engine/Definition/DefinitionCompiler.cs`, `engine/Statevia.Core.Engine/Abstractions/CompiledWorkflowDefinition.cs`_
  - _Requirements: Requirement 1, Requirement 2, Requirement 4_
  - _Definition of Done: コンパイル結果に条件遷移情報が保持され、既存 next/fork/end も後方互換で維持される_

- [x] **T5** — Engine の条件遷移評価を実装
  - File: `engine/Statevia.Core.Engine/Engine/WorkflowEngine.cs`, `engine/Statevia.Core.Engine/FSM/Fact.cs`
  - 内容: `output` に対する `when.path/op/value` 評価、`order` による選択、`first-match wins`、`default` フォールバック、`in` / `between` の評価を実装する
  - 目的: State 実行結果に応じた次 state 選択をエンジンで実現する
  - _Leverage: `engine/Statevia.Core.Engine/Engine/WorkflowEngine.cs`, `engine/Statevia.Core.Engine/FSM/Fact.cs`_
  - _Requirements: Requirement 2, Requirement 3, Requirement 5_
  - _Definition of Done: 条件一致・未一致・default フォールバック・比較演算が仕様どおり動作する_

- [x] **T6** — エラー可視化とデバッグ返却方針を反映
  - File: `engine/Statevia.Core.Engine/Engine/WorkflowEngine.cs`, `api/Statevia.Core.Api/Services/DefinitionService.cs`, `api/Statevia.Core.Api/Hosting/DefinitionCompilerService.cs`
  - 内容: Engine は既存エラー配列方針を維持しつつ条件評価エラーを返し、API はデバッグ用途で評価対象 case・採用結果・no-match 理由を返却できるようにする
  - 目的: 条件遷移の不一致や評価不能を運用で追跡しやすくする
  - _Leverage: `engine/Statevia.Core.Engine/Engine/WorkflowEngine.cs`, `api/Statevia.Core.Api/Hosting/DefinitionCompilerService.cs`_
  - _Requirements: Requirement 3, Non-Functional（Observability）_
  - _Definition of Done: Engine と API の返却方針が仕様と一致し、UI が利用できる評価情報が揃う_

- [x] **T7** — 単体テストを追加
  - File: `engine/Statevia.Core.Engine.Tests/Definition/StateWorkflowDefinitionLoaderTests.cs`, `engine/Statevia.Core.Engine.Tests/Definition/DefinitionValidatorTests.cs`, `engine/Statevia.Core.Engine.Tests/Definition/DefinitionCompilerTests.cs`, `engine/Statevia.Core.Engine.Tests/Engine/WorkflowEngineTests.cs`
  - 内容: `cases/default`、`default` ショートハンド、`nodes.next` / `nodes.edges` 互換、`in`、`between`、`order`、混在禁止、不正 path、不正 value 形状の単体テストを追加する
  - 目的: 仕様追加による回帰を局所的に防ぐ
  - _Leverage: 既存の Definition / Engine テスト群_
  - _Requirements: Requirement 1-5, Non-Functional（Reliability）_
  - _Definition of Done: 正常系・異常系の主要ケースがテスト化され、変更範囲の回帰を検出できる_

- [x] **T8** — API 経路の統合確認と UI 表示契約確認
  - File: `api/Statevia.Core.Api.Tests/`, `services/ui/` 関連実装またはテスト
  - 内容: 定義登録 API の 422 応答、デバッグ返却、UI が API 返却値を再評価せず表示する契約を確認し、必要に応じてテストや最小実装を追加する
  - 目的: Engine だけでなく API / UI 境界でも仕様を崩さないようにする
  - _Leverage: `api/Statevia.Core.Api/Services/DefinitionService.cs`, 既存 API/UI テスト基盤_
  - _Requirements: Requirement 3, Non-Functional（Observability）_
  - _Definition of Done: API と UI の境界で条件評価結果の扱いが仕様どおり確認できる_

- [x] **T9** — 最終検証と docs 同期準備
  - File: `docs/core-engine-definition-spec.md`（実装後同期）, `.spec-workflow/specs/nodes-output-conditional-routing/*.md`
  - 内容: `dotnet test` の該当範囲を実行し、実装差分を spec に反映したうえで、実装完了後に `docs/` へ同期する差分一覧を整理する
  - 目的: spec を正にしつつ、実装完了後の docs 更新を漏れなく行えるようにする
  - _Leverage: `docs/core-engine-definition-spec.md`, `.spec-workflow/specs/nodes-output-conditional-routing/requirements.md`, `.spec-workflow/specs/nodes-output-conditional-routing/design.md`_
  - _Requirements: Non-Functional（Documentation Lifecycle）, Non-Functional（Reliability）_
  - _Definition of Done: 該当テストが通り、実装後に `docs/` へ反映すべき内容が明文化されている_

## T9 実施結果（2026-04-21）

- 実行テスト:
  - `dotnet test .\\engine\\statevia-engine.sln --filter "FullyQualifiedName~StateWorkflowDefinitionLoaderTests|FullyQualifiedName~Level1ValidationTests|FullyQualifiedName~DefinitionValidatorTests|FullyQualifiedName~DefinitionCompilerTests|FullyQualifiedName~WorkflowEngineTests|FullyQualifiedName~WorkflowEngineLoggingTests"`（合格: 91）
  - `dotnet test .\\api\\statevia-api.sln --filter "FullyQualifiedName~DefinitionCompilerServiceTests|FullyQualifiedName~WorkflowsControllerTests.GetState_ReturnsOkWorkflowView"`（合格: 17）
  - `npm run test:run -- tests/lib/workflowView.test.ts tests/features/execution/useExecution.test.ts`（合格: 29）
- spec 反映:
  - `design.md` の Error Visibility Policy 配下に実装同期メモを追加（Engine/API/UI の実装到達点と T10 への残課題を明記）
- docs 同期（`docs/core-engine-definition-spec.md` 予定差分）:
  - `conditionRouting` の実行グラフ返却仕様（キー、意味、no-match 時の扱い）
  - `compiledJson` に `conditionalTransitions` / `stateInputs` を含めるデバッグ契約
  - UI が `conditionRouting` を再評価せず透過表示する境界契約
  - JSON キー命名は T10 で camelCase 統一予定である旨

- [x] **T10** — JSON 出力命名を camelCase に統一
  - File: `engine/Statevia.Core.Engine/ExecutionGraph/ExecutionGraph.cs`, `api/Statevia.Core.Api/Hosting/DefinitionCompilerService.cs`, `api/Statevia.Core.Api/Services/WorkflowViewMapper.cs`, 関連テスト
  - 内容: Engine の `ExportExecutionGraph` と API が返すデバッグ用 JSON（`compiledJson` 含む）で命名ポリシーを camelCase に統一し、既存パーサ依存箇所を移行する
  - 目的: 出力経路ごとの PascalCase / camelCase 混在を解消し、契約の一貫性を確保する
  - _Leverage: `engine/Statevia.Core.Engine/ExecutionGraph/ExecutionGraph.cs`, `api/Statevia.Core.Api/Hosting/DefinitionCompilerService.cs`_
  - _Requirements: Non-Functional（Clarity）, Non-Functional（Observability）_
  - _Definition of Done: 実行グラフ JSON とコンパイル済み JSON のキー名が camelCase で統一され、回帰テストで固定化されている_

## T10 実施結果（2026-04-21）

- 実装:
  - `ExecutionGraph.ExportJson` の `JsonSerializerOptions` に `PropertyNamingPolicy = JsonNamingPolicy.CamelCase` を適用
  - `DefinitionCompilerService.ValidateAndCompile` の `compiledJson` 生成に camelCase ポリシーを適用
  - `WorkflowViewMapper` の実行グラフ JSON パーサを camelCase 契約に寄せるため、`JsonPropertyName` 属性で受け口を明示
- 回帰テスト:
  - `dotnet test .\\engine\\statevia-engine.sln --filter "FullyQualifiedName~Start_ConditionalTransition_ExportsRoutingDiagnosticsOnGraph"`（合格: 1）
  - `dotnet test .\\api\\statevia-api.sln --filter "FullyQualifiedName~DefinitionCompilerServiceTests.ValidateAndCompile_CompiledJson_IncludesConditionalTransitionsAndStateInputs|FullyQualifiedName~WorkflowsControllerTests.GetState_ReturnsOkWorkflowView"`（合格: 2）
  - `npm run test:run -- tests/lib/workflowView.test.ts`（合格: 1）

---

## 実行メモ

- 着手中は `[ ]` を `[-]`、完了後は `[x]` に更新する。
- 実装順は `T1 -> T2 -> T3 -> T4 -> T5 -> T6 -> T7 -> T8 -> T9 -> T10` を基本とする。
- `docs/` 配下の更新は `T9` 以降、実装とテスト完了後に行う。
