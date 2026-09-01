# 未実装の Push と投影拡張

| 項目 | 値 |
| --- | --- |
| 種別 | Future |
| Version | 1.0 |
| 更新日 | 2026-09-01 |
| 関連 | [../specifications/ui/push-api.md](../specifications/ui/push-api.md), [../specifications/data-integration.md](../specifications/data-integration.md), [../specifications/platform/execution-security-snapshot.md](../specifications/platform/execution-security-snapshot.md) |

---

契約・実装の正本ではない。現行の SSE は `GraphUpdated` のみ。投影の正本は `executions` と `execution_graph_snapshots`。

## Push 種別（未実装）

現行 SSE が送出するのは `GraphUpdated` だけである。次は載っていない。

- `EXECUTION_UPDATED` / JSON Patch（旧 UI-UPDATE / UI-PATCH）
- `ExecutionStatusChanged` / `NodeCancelled` / `NodeFailed` を独立イベントとして送ること
- 複数 Execution のストリーム購読、GraphUpdated の JSON Patch 化

導入するときは [push-api.md](../specifications/ui/push-api.md) と [data-integration.md](../specifications/data-integration.md) を現行契約として更新する。

## 投影キューとバッチ再送（未実装）

ノード完了ごとの投影デバウンス、有界キュー、案 C の `batchId` 再送は未実装。現行は HTTP コマンド経路の同一トランザクション更新と、実装済みの投影キュー（テナント文脈付き）に従う。

## Execution Security Snapshot の先

Engine への Snapshot 注入、permission key の細分化（`executions.manage` 等）、Execution Liveness Policy、Snapshot-only Operator は未実装。現行の Owner / Operator 評価は [execution-security-snapshot.md](../specifications/platform/execution-security-snapshot.md)。
