# FSM 仕様

Version: 1.0
Project: 実行型ステートマシン

---

statevia は事実駆動型有限状態機械を使用します。

## コアコンセプト

遷移は以下によって評価されます：

(状態, 事実) -> 遷移結果

事実は実際の実行結果を表します：

- Completed（完了）
- Failed（失敗）
- Cancelled（キャンセル済み）
- Joined（結合済み）

リクエスト（例：キャンセルリクエスト）は事実ではありません。

### Wait 状態と事実（実装に準拠）

YAML の `wait.event`（例: `payment.completed`）は **外部から `PublishEvent` されたイベント名**と突き合わせ、`IEventProvider.WaitAsync` が再開するために使う識別子である（`EventProvider` / `WaitOnlyState`）。

待機が解けて状態の `ExecuteAsync` が正常終了したあと、エンジンが FSM に渡す事実は **`Completed` 固定**である。`WorkflowEngine.ScheduleStateAsync` は executor 成功時に `ProcessFact(..., Fact.Completed, ...)` とし、`TransitionTable.Evaluate(stateName, "Completed")` で `on` を引く。

したがって Wait 状態の遷移は **`on.Completed`（例: `Completed: { next: ... }`）** で記述する。**`on.<wait.event と同じ文字列>` は現行実装では評価されない**（イベント名と FSM の事実名を混同しないこと）。

## FSM の特性

- 決定論的遷移
- 暗黙の状態遷移なし
- 自己遷移は禁止
- 遷移は O(1) ルックアップテーブルに事前コンパイルされる

## 遷移結果

遷移結果は以下を行うことができます：

- 新しい状態を開始する
- 現在の状態を終了する
- Fork または Join ロジックをトリガーする
