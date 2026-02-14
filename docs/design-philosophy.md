# Design Philosophy

statevia is designed around the following principles:

- Definition-driven workflow
- Fact-driven FSM
- Fork / Join as control nodes, not states
- Explicit dependency declaration
- Asynchronous execution with cooperative cancellation
- Safety-first design
- Engine does not interfere with user logic
- Observation and execution are strictly separated

## Key Decisions

### Fact-driven Transitions

Only actual results (facts) trigger state transitions.
Requests (e.g., cancellation requests) are not considered facts.

### Non-intrusive Engine

The engine never forcefully aborts state execution.
Cancellation is cooperative and handled by user code.

### Formal Structure

The workflow definition is treated as a formal specification,
not just a configuration file.

---

# 日本語

## 設計哲学

statevia は以下の原則に基づいて設計されています：

* 定義駆動型ワークフロー
* 事実駆動型 FSM
* Fork / Join を制御ノードとして（状態ではない）
* 明示的な依存関係宣言
* 協調的キャンセルによる非同期実行
* セーフティファースト設計
* エンジンはユーザーロジックに干渉しない
* 観測と実行は厳密に分離される

## 主な決定事項

### 事実駆動型遷移

実際の結果（事実）のみが状態遷移をトリガーします。リクエスト（例：キャンセルリクエスト）は事実とはみなされません。

### 非侵入型エンジン

エンジンは状態実行を強制終了しません。キャンセルは協調的であり、ユーザーコードによって処理されます。

### 形式的な構造

ワークフロー定義は、単なる設定ファイルではなく、形式的な仕様として扱われます。
