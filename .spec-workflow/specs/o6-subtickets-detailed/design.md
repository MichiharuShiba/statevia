# Design: O6 サブチケット詳細仕様（STV-413〜STV-418）

## Overview

本 design は **`.workspace-docs/30_specs/10_in-progress/o6-subtickets_detailed_spec.md`** と同一の技術判断を要約する。**図表・Mermaid・長文表はワークスペース正本に集約**し、本ファイルは spec-workflow のレビュー用入口とする。

### 正本との関係

| 本 design の節 | ワークスペース正本の対応 |
|----------------|--------------------------|
| projection / TX | `o6-subtickets_detailed_spec.md` の STV-413 |
| event_store 種別 | 同 STV-414 |
| 再送べき等 | 同 STV-415 |
| GetSnapshot / reducer | 同 STV-416 |
| nodes 段階 | 同 STV-417 |
| 横断チェックリスト | 同 STV-418 |

### 現状アーキテクチャ（要点）

- Core-API は **コマンド同期経路のみ**（`WorkflowService` が Engine を in-process 呼び出し後、`GetSnapshot` / `ExportExecutionGraph` で projection を構築して DB に書く）。
- `event_store` に載る種別は現行 **`WorkflowStarted` / `WorkflowCancelled` / `EventPublished`** の 3 種（`EventStoreEventType`）。
- U1 で想定される **非同期コールバック + 案 C バッチ**は未実装。導入時は **1 バッチ = 1 トランザクション**（INSERT → reducer → projection）で STV-413 と揃える。

### 実装参照（コード）

- `api/Statevia.Core.Api/Services/WorkflowService.cs`
- `api/Statevia.Core.Api/Abstractions/Persistence/EventStoreEventType.cs`

## References

- `.workspace-docs/30_specs/10_in-progress/o6-subtickets_detailed_spec.md`
- `.workspace-docs/40_plans/10_in-progress/v2-modification-plan.md`（8.2 懸念表・O6 参照行）
- `.workspace-docs/30_specs/10_in-progress/v2-nodes-to-states-conversion-spec.md`
