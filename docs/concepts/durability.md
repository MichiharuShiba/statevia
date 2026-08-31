# 永続化とイベント

| 項目 | 値 |
| --- | --- |
| 種別 | Concept |
| Version | 1.5.5 |
| 更新日 | 2026-09-01 |
| 関連 | [../specifications/data-integration.md](../specifications/data-integration.md), [../specifications/execution/fork-join.md](../specifications/execution/fork-join.md) |

---

Statevia は実行の進行を **PostgreSQL** 上の projection と **event_store** で支えます。Engine 自体はインメモリで動きますが、Service API がトランザクション境界を持ち、開始・キャンセル・publish 等のミューテーションを durable に記録します。Wait で停止した実行はランタイムチェックポイントから hydrate でき、非同期の再開要求は耐久ワークキューで配送します。

チェックポイント本体は Application の **`IExecutionCheckpointStore`**（文書キー = `executionId`）経由でのみ扱います。現行実装は Postgres テーブル `execution_runtime_checkpoints` ですが、契約は RDB 行ではなく文書ストアとして定義しており、将来 MongoDB / DynamoDB 等へ差し替え可能な境界にしています。

## 何が正本か

- **実行の read-model**（一覧・GET execution・GET graph）: `executions` と `execution_graph_snapshots` 等の DB projection
- **定義**: `definitions` / `definition_versions`（immutable 版）
- **イベント履歴**: `event_store` への append（同一トランザクション内で projection と整合）
- **Hosted Fork の親子リンク / Join 充足**: `execution_branches`（親の durable Wait ではない）

in-process の `GetSnapshot` はデバッグやコールバック経路向けであり、HTTP API の正本ではありません。

親の `GET …/graph` / `GET …/events` は、物理子を論理 1 実行に見せるため **GET 時合成**する。永続 snapshot / `event_store` への親への二重書き込みはしない（契約の詳細は [fork-join.md](../specifications/execution/fork-join.md)）。

## 開始とミューテーション

**Start** は ReadCommitted の 1 トランザクションで executions・スナップショット・カーソル・wait・event_store（と必要なら command_dedup）をまとめて commit します。HTTP が受理したあと Worker が queued Start を処理します。再 claim 時に非空 graph・正当な runtime checkpoint・同一プロセスの Engine 載荷のいずれかがあれば `engine.Start` しません（Complete のみ）。空グラフかつ checkpoint なしなら Start します。

**Cancel / Publish** 等は受信記録（tx1）と Serializable リトライ付きの投影更新（tx2）に分かれる経路があります。詳細な順序とフィールドは Specification を参照してください。

## Wait と cursor

`execution_cursors` と `execution_waits`（EventWait の durable wait）は、executions 更新と**同一トランザクション**で同期します。cursor は投影キューと checkpoint 永続化が重なっても PK の原子 upsert で 1 行に収束します。read-model の GET は cursor に依存せず、グラフスナップショットを正とします。

`execution_work_items` の kind は **Start / Resume / Cancel** です（Delay 期限も `Resume` + `mode=event`）。複数の API プロセスは PostgreSQL の `FOR UPDATE SKIP LOCKED` により同一項目を同時処理しません。1 ワーカープロセスは `Statevia:Runtime:Worker:MaxConcurrency`（既定 1）まで Start / Resume を同時処理し、Cancel は独立ループです。同一プロセスが Engine に載荷済みなら Cancel は先に Engine `CancelAsync`（Action CT と次遷移抑制）を呼び、その後 AwaitLoad を IRQ します。処理中は work item lease と checkpoint 所有 lease を heartbeat 延長します。非 Wait Running が無い無進捗が `NoProgressTimeout` を超えると Unload し work item を Complete します（実行行は当面 Running）。保存版の Restore 不能など直らない失敗は Release せず Complete し、未載荷の Start / Resume なら投影を `Failed` にします。未分類例外は `MaxAttempts`（既定 20）で止めます。ただし **開始済み Resume**（非空 graph または正当な runtime checkpoint）の未分類上限は投影を `Failed` にせず checkpoint を残し、既存の `restartLost` を立てます。所有獲得失敗は上限に含めず、claim で増えた `attempts` を戻して Release します。投影キューは現状グローバル直列です。

実行中の所有正本は `execution_runtime_checkpoints` の `owner_worker_id` / `lease_until` / `owner_generation` です。`lease_until` は recovery 検討のトリガに過ぎず、排他は **`owner_generation` 一致更新（fencing）** で担保します。ハートビートまたは世代付き更新が失敗した Worker はローカル実行を即停止します。`ExecutionOwnershipRecoveryHostedService` は期限切れ所有を世代 +1 したうえで `Resume mode=recovery` を enqueue します（Wait イベントは消費しない）。DelayWait 期限は `DelayWaitSchedulerHostedService` が `FOR UPDATE SKIP LOCKED` で wait を排他 claim し、同一トランザクションで wait 削除と `Resume mode=event` を投入します。これらの HostedService は既定では Service API 内で動きますが、`Statevia:Runtime` で無効化し `service/runtime` の Scheduler / Worker へ分離できます。

ステップ完了ごとに checkpoint を更新し、Unload は durable Wait 到達・物理 Join 待ち解消後の不要時・または終端に限定します。recovery で Action が再実行されうる場合、Action 側の冪等（または適用済み検知）を前提とします。

## runtime checkpoint 寿命表

内容の保持理由は次の 2 つのみ（Application: `RuntimeCheckpointRetainReasons`）。

| 保持理由 | 意味 |
| --- | --- |
| `PendingWaits` | グラフ上に Engine durable Wait がある |
| `PendingPhysicalJoin` | 親に未終端の物理分岐（`execution_branches.status=Running`）がある |

- **内容破棄候補**: 投影が終端、または Running かつ上記理由が空。
- **OwnedLease（Worker 所有）は同列にしない**。内容破棄候補でも所有中は行を Delete せず runtime JSON のみ refresh する（lease / fencing 都合）。

Hosted Fork の詳細は [fork-join.md](../specifications/execution/fork-join.md)。

## Hosted Fork と耐久

- Fork 展開・子 Start・親子リンクは `execution_branches` と work item で耐久化する。展開一時失敗はリトライし、上限後に親 Failed。
- 子終端は親向け予約 Resume（`statevia.event.child.completed`）で起こし、Join を再評価する。
- ネスト Fork も同一経路で再帰分割する。
- 親の runtime checkpoint は上記寿命表に従う。`BeginOwnedSession` の seed `{}` は lease 用であり hydrate 正本にしない。未 Start の Cancel はこの seed / 欠落 checkpoint を Import せず、投影を `Cancelled` にして残 Start work item を Complete する。
- 合成読みモデルの辺規則（枝先頭のみ Fork、内側 Join→外側は Next）は [fork-join.md](../specifications/execution/fork-join.md) を正とする。

## 次に読むもの

- データ連携契約: [specifications/data-integration.md](../specifications/data-integration.md)
- Fork / Join（物理子・合成読みモデル）: [specifications/execution/fork-join.md](../specifications/execution/fork-join.md)
- DB スキーマ参照: [reference/database-schema.md](../reference/database-schema.md)
- Event Store の設計判断: [decisions/event-store.md](../decisions/event-store.md)
