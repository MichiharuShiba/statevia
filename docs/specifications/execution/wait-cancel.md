# Wait / Cancel 仕様

| 項目 | 値 |
| --- | --- |
| 種別 | Specification |
| Version | 1.0 |
| 更新日 | 2026-07-07 |
| 関連 | [fsm.md](fsm.md), [concepts/execution-model.md](../../concepts/execution-model.md) |

---

## Normative 要約

- **MUST**: Wait は指定イベント一致まで実行を一時停止する。各 Wait は一度だけ Resume できる。
- **MUST**: Cancel は協調的とし、エンジンは状態実行を強制終了してはならない。
- **MUST**: `Cancelled` 事実は実行が実際に停止したときのみ発行する。
- **SHOULD**: 依存状態はキャンセル伝播の対象とする。

---

## Wait（待機）

Wait は指定されたイベントが発生するまで状態実行を一時停止します。

- Wait は待機スコープを導入します。
- Resume は同じ状態実行を継続します。
- 各 Wait は一度だけ再開できます。
- **`wait.event`**: `PublishEvent` のイベント名と一致したときに待機が解ける（`WaitOnlyState` / `EventProvider`）。
- **FSM の事実**: 待機解消後に状態実行が正常終了すると、遷移評価に使われる事実は **`Completed`** である。定義の `on` は **`Completed` キー**で次遷移を書く（[fsm.md](fsm.md) の「Wait 状態と事実」を参照）。

## Cancel（キャンセル）

- キャンセルは協調的です。
- エンジンは実行中の状態を強制終了しません。
- Cancelled は実行が実際に停止したときにのみ発行されます。
- 依存状態は自動的にキャンセルされます。

## 状態

- 待機状態は再開されるまで完了しません。
- すべてのアクティブな状態が待機中の場合、ワークフローは一時停止とみなされます。
