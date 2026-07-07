# 実行モデル

| 項目 | 値 |
| --- | --- |
| 種別 | Concept |
| Version | 1.0 |
| 更新日 | 2026-07-07 |
| 関連 | [../specifications/execution/](../specifications/execution/) |

---

1 回の **Execution** は、定義版に従って進む状態機械のインスタンスです。外部から見ると「開始 → 進行 → 完了／失敗／キャンセル」というライフサイクルですが、内部ではイベント駆動の reducer がグラフ上のノード状態を更新し続けます。

## 章立て: 実行が進む流れ

### 1. 開始（Start）

クライアントが Command として実行開始を要求します。API は検証のうえ Engine を起動し、永続化（executions、スナップショット、カーソル、event_store 等）を同一トランザクションで行います。

### 2. 状態のスケジュールと事実

各 **State**（または制御ノード）はスケジュールされ、Action 実行や Wait が発生します。完了・失敗・タイムアウトなどの**事実（Fact）**が reducer に渡され、次の遷移が決まります。

### 3. FSM と reducer

遷移は `(状態, 事実) → 結果` として評価されます。reducer は副作用を持たない純粋な更新として設計され、Command は必ず Event に変換されたうえで適用されます。

### 4. Fork / Join

並列分岐と合流は**制御ノード**としてグラフに表現されます。Join は依存する分岐の完了事実を待ち、すべて揃った時点で次へ進みます。

### 5. Wait と Cancel

EventWait など durable な待機は operational projection と整合します。キャンセルは協調的: 要求は Command として受理されますが、遷移はキャンセルが処理された**事実**まで保留されます。

### 6. 実行グラフの可視化

Engine は実行時グラフ（ノード ID、状態名、attempt、waitKey 等）をエクスポートします。HTTP GET の read-model は DB projection を正とし、in-process スナップショットと一致しない場合がある点に注意してください。

## Specification への対応

| トピック | 正本 |
| --- | --- |
| FSM・状態機械・reducer | [execution/fsm.md](../specifications/execution/fsm.md) |
| イベント・コマンド | [execution/events-and-commands.md](../specifications/execution/events-and-commands.md) |
| Wait / Cancel | [execution/wait-cancel.md](../specifications/execution/wait-cancel.md) |
| Fork / Join | [execution/fork-join.md](../specifications/execution/fork-join.md) |
| ExecutionGraph JSON | [execution/execution-graph.md](../specifications/execution/execution-graph.md) |

## 次に読むもの

- データ連携のタイミング: [durability.md](durability.md)
- 定義の書き方: [definition.md](definition.md)
