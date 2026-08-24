# Wait / Cancel 仕様

| 項目 | 値 |
| --- | --- |
| 種別 | Specification |
| Version | 1.5 |
| 更新日 | 2026-08-24 |
| 関連 | [fsm.md](fsm.md), [../definition.md](../definition.md), [fork-join.md](fork-join.md), [concepts/execution-model.md](../../concepts/execution-model.md) |

---

## Normative 要約

- **MUST**: Wait は許可イベントのいずれかが発生するまで実行を一時停止する。各 Wait ノードは一度だけ Resume できる。
- **MUST**: 再開の正本は **ノードスコープ**（`executionId` + `nodeId` + `eventName`）。HTTP では `POST …/nodes/{nodeId}/resume`、`resumeKey` = イベント名。
- **MUST**: Cancel は協調的とし、エンジンは状態実行を強制終了してはならない。
- **MUST**: `Cancelled` 事実は実行が実際に停止したときのみ発行する。
- **SHOULD**: 依存状態はキャンセル伝播の対象とする。
- **禁止**: 利用者向け文書に `exit` 語彙を出さない。

---

## Wait（待機）

Wait は定義の許可イベントのいずれかが発生するまで状態実行を一時停止します。

- Wait は待機スコープを導入します（実行グラフ上は 1 Wait ノード = 1 待機）。
- Resume は同じ状態実行を継続します。
- 各 Wait は一度だけ再開できます。
- **定義**: `wait.events`（states）または nodes の `events`。コンパイル結果は **`WaitEventRouteTable`**。
- **再開正本**: Engine `ResumeWaitNode(executionId, nodeId, eventName)`。`EventProvider` はノード単位の `WaitForEventAsync` / `Resume`。
- **耐久配送**: `execution_work_items` の `Resume`（`mode=event`）は worker が checkpoint を hydrate した上でこの再開正本を呼び出す。HTTP Resume は当面、同じ正本を同期呼び出しする。Worker は claim 1 件ずつ処理し、処理中は work item / checkpoint 所有の lease を heartbeat 延長する。プロセス死亡時は heartbeat 停止後に checkpoint `lease_until` 切れを Scheduler が検知し、`Resume mode=recovery` で再割当する。
- **checkpoint**: ステップ完了ごとに runtime checkpoint を更新する。durable Wait 到達時は waits 投影・checkpoint 保存のうえ Engine から Unload し所有を解放する（suspend 通知でも同処理）。終端では不要な checkpoint を破棄する。
- **DelayWait**: 期限到達時は `Resume mode=event` を enqueue し、`eventName` は固定の `statevia.event.delay.completed`（`ExecutionWaitEventNames.DelayCompleted`）。`allowed_events` の先頭要素は使わない。
- **許可外イベント・非アクティブ Wait・不明 nodeId**: 422（`InvalidOperationException` → API `ApiValidationException`）。
- **FSM の事実**: 待機解消後に状態実行が正常終了すると、JoinTracker 等へ渡す事実は **`Completed`**。次状態の解決は **イベント名 + route table**（[fsm.md](fsm.md)）。
- **監査**: Wait 完了 output に `{ "event": "<eventName>" }` を載せ得る（遷移判定には使わない）。

### PublishEvent 互換シム

`POST /v1/executions/{id}/events`（Engine `PublishEvent`）は **非推奨の互換経路**である。

| 条件 | 結果 |
| --- | --- |
| アクティブ Wait がちょうど 1 ノード、かつ許可イベントがちょうど 1 件、かつ `name` がそのイベントと一致 | `ResumeWaitNode` 相当で再開 |
| 上記以外（複数 Wait / 複数 events / イベント不一致 等） | **422** |

Fork 並列 Wait や複数イベント Wait では必ず Resume（nodeId + eventName）を使う。

### Hosted 物理 Fork での配送

Hosted では分岐が物理子 execution になるため、分岐上の Wait へのイベント／Resume は**子の `execution_id`** を正とする。親 ID 宛てに届いた場合は子へ解決し、解決不能なら 422。topic ingress（Subscribe）も購読行の子 `execution_id` を用いる。表示用の親イベントタイムライン合成とは別関心である（[fork-join.md](fork-join.md)）。

### Topic / key ingress（Subscribe）

`POST /v1/events` は `{ topic, key?, payload? }` を受け付けます（`topic` 必須。`event` は送らない）。照合は `execution_wait_subscriptions` で、正規化後の **topic かつ key の厳密一致**です。`key` 省略・空白は `""` です。一致した各購読の `resume_event_name` で Resume ワークを投入し、対象が 0 件でも `204 No Content` を返します。`payload` は将来の Wait 入力拡張のため受理しますが、この段階では再開値に反映しません。

Wait 定義側は `wait.subscribe`（topic 必須・key 任意・next 必須）。Signal モード（`wait.events` のみ）は本 API の対象外で、execution-scoped Resume を使います。

Subscribe は入れ子ワークフローの **公式な子→親 Resume ではない**。同じ topic / key を子や外部が発行すれば親 wait を進められる、という合成にすぎません。一致した購読が複数あれば **1:N** で再開します。1:1 に抑えたい場合は作者が key を十分一意にしてください。専用 Action `execution.resume` は提供しません。

## Cancel（キャンセル）

- キャンセルは協調的です。
- エンジンは実行中の状態を強制終了しません。
- Cancelled は実行が実際に停止したときにのみ発行されます。
- 依存状態は自動的にキャンセルされます。
- Hosted で親を Cancel した場合、未終端の物理子へ Cancel work item をカスケードする（[fork-join.md](fork-join.md)）。

## 状態

- 待機状態は再開されるまで完了しません。
- すべてのアクティブな状態が待機中の場合、ワークフローは一時停止とみなされます。
- Read model の未完了 Wait ノード status は **`WAITING`**（`allowedEvents` を UI 表示に利用）。
