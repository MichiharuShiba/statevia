# 永続化とイベント

| 項目 | 値 |
| --- | --- |
| 種別 | Concept |
| Version | 1.0 |
| 更新日 | 2026-07-07 |
| 関連 | [../specifications/data-integration.md](../specifications/data-integration.md) |

---

Statevia は実行の進行を **PostgreSQL** 上の projection と **event_store** で支えます。Engine 自体はインメモリで動きますが、Core-API がトランザクション境界を持ち、開始・キャンセル・publish 等のミューテーションを durable に記録します。

## 何が正本か

- **実行の read-model**（一覧・GET execution・GET graph）: `executions` と `execution_graph_snapshots` 等の DB projection
- **定義**: `definitions` / `definition_versions`（immutable 版）
- **イベント履歴**: `event_store` への append（同一トランザクション内で projection と整合）

in-process の `GetSnapshot` はデバッグやコールバック経路向けであり、HTTP API の正本ではありません。

## 開始とミューテーション

**Start** は ReadCommitted の 1 トランザクションで executions・スナップショット・カーソル・wait・event_store（と必要なら command_dedup）をまとめて commit します。

**Cancel / Publish** 等は受信記録（tx1）と Serializable リトライ付きの投影更新（tx2）に分かれる経路があります。詳細な順序とフィールドは Specification を参照してください。

## Wait と cursor

`execution_cursors` と `execution_waits`（EventWait の durable wait）は、executions 更新と**同一トランザクション**で同期します。read-model の GET は cursor に依存せず、グラフスナップショットを正とします。

## 次に読むもの

- データ連携契約: [specifications/data-integration.md](../specifications/data-integration.md)
- DB スキーマ参照: [reference/database-schema.md](../reference/database-schema.md)
- Event Store の設計判断: [decisions/event-store.md](../decisions/event-store.md)
