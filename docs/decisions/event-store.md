# Event Store を投影の正本としない

| 項目 | 値 |
| --- | --- |
| 種別 | Decision |
| 更新日 | 2026-07-07 |
| ステータス | 採用 |

## Context

イベントソーシングのみを read-model の正本にすると、HTTP GET のレイテンシと運用複雑度が増える。一方、projection のみでは履歴の完全性が弱い。

## Decision

- **HTTP read-model**（`GET /v1/executions` / graph）は **`executions` + `execution_graph_snapshots` 等の DB projection を正**とする
- **`event_store`** は append-only の履歴・再投影の素材とし、通常の GET パスでは replay しない
- ミューテーション（Start / Cancel 等）は projection と **同一トランザクション**で `event_store` に append する

## Consequences

- クライアントは単純な GET 契約を維持できる
- projection と event_store の二重管理が必要（整合はトランザクション境界で担保）
- in-process の Engine スナップショットはデバッグ用途であり API 正本ではない

関連: [durability.md](../concepts/durability.md)、[data-integration.md](../specifications/data-integration.md)
