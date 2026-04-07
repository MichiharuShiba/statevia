# Tasks Document: Warning Policy (STV-405)

- [ ] 1. WorkflowExecutionLogger への LogWarning メソッド追加
  - File: `engine/Statevia.Core.Engine/Engine/WorkflowEngine.Logging.cs`
  - `LogWarningInputEvaluation` と `LogWarningNoTransition` を実装する。
  - 例外が外に漏れるのを防ぐため、必ず `SafeLog()` でラップする。
  - Purpose: 正しい構造で警告ログを出すための、安全なラッパーメソッドを提供する。
  - _Leverage: ILogger.LogWarning, SafeLog_
  - _Requirements: Requirement 1, Requirement 2_
  - _Prompt: Role: C# 開発エンジニア | Task: STV-405 に従い、`WorkflowExecutionLogger` に `LogWarningInputEvaluation` および `LogWarningNoTransition` メソッドを追加してください。「spec-workflow-guide」を事前に実行してください。 | Restrictions: 絶対に例外を投げないでください。 | Success: 既存の SafeLog パターンを用いて新メソッドが定義されること。_

- [ ] 2. Input 評価時の警告（Warning）検知と発火の実装
  - File: `engine/Statevia.Core.Engine/Engine/WorkflowEngine.cs`
  - `ApplyStateInput` における評価時に、フォールバックや問題が起きていないかフックする。
  - 問題があれば `_executionLogger.LogWarningInputEvaluation` を呼び出す。
  - Purpose: State の input 解決時に代替対応が必要だったことをログに残す。
  - _Leverage: StateInputEvaluator, WorkflowExecutionLogger_
  - _Requirements: Requirement 1_
  - _Prompt: Role: C# 開発エンジニア | Task: WorkflowEngine内に STV-405 の Input 評価警告を実装してください。フォールバック処理を検知し `LogWarningInputEvaluation` を呼ぶようにします。「spec-workflow-guide」を事前に実行してください。 | Restrictions: ログ出力の追加がワークフローの実行状態に副作用を与えないこと。 | Success: フォールバック時のみ正しく警告ログが呼ばれること。_

- [ ] 3. 遷移なし停止時の警告（Warning）検知と発火の実装
  - File: `engine/Statevia.Core.Engine/Engine/WorkflowEngine.cs`
  - `ProcessFact` 内で `evaluatedTransition` の結果を検証する。
  - `HasTransition == false` であり、かつ End 状態や正常な Join 待機でもない場合は `_executionLogger.LogWarningNoTransition` を呼ぶ。
  - Purpose: 明確な遷移先を持たずに実行が事実上ストップした状況を記録する。
  - _Leverage: WorkflowExecutionLogger_
  - _Requirements: Requirement 2_
  - _Prompt: Role: C# 開発エンジニア | Task: WorkflowEngine内に遷移不能時（No Transition）の警告を実装します。EndやJoinのような正常な停止以外で遷移先が無かった場合にログを出力させます。「spec-workflow-guide」を事前に実行してください。 | Restrictions: 正常な待機（False positive）で警告が誤爆しないこと。 | Success: 処理不能な Fact の投入に対して警告が出力されること。_

- [ ] 4. 警告ログ用の単体テスト追加
  - File: `engine/Statevia.Core.Engine.Tests/Engine/WorkflowEngineLoggingTests.cs`
  - Input 警告に対するテストケース、および No Transition に対するテストケースをそれぞれ最低 1 件作成する。
  - モック化した `ILogger` を用いて、警告が意図したタイミングで出力を要求されたか検証（Verify）する。
  - Purpose: 将来のリファクタリングで警告ログの機能が壊れないことを保証する。
  - _Leverage: 既存のテストデータやモック機能_
  - _Requirements: Requirement 3_
  - _Prompt: Role: C# テストエンジニア | Task: STV-405の警告要件（Input評価例外時、遷移不能時）を網羅する単体テストを追加してください。「spec-workflow-guide」を事前に実行してください。 | Restrictions: 既存のテストスイートを破壊しないこと。 | Success: 各警告実行パスを通過し、テストがGreenで通ること。_
