# ドメインモデル関連図（境界付き）

関連する概念が増えてきたため、Statevia の主要ドメインモデルを「どの境界に属するか」と「どこが正本か」を含めて俯瞰できるように整理する。

## 1. ドメイン境界と関連

```mermaid
flowchart LR
  classDef domain fill:#eef7ff,stroke:#1d4ed8,stroke-width:1px,color:#0f172a;
  classDef integration fill:#eefcf2,stroke:#16a34a,stroke-width:1px,color:#052e16;
  classDef storage fill:#fff7ed,stroke:#ea580c,stroke-width:1px,color:#431407;
  classDef external fill:#f8fafc,stroke:#475569,stroke-width:1px,color:#0f172a;

  subgraph UI["UI境界 (Next.js)"]
    UICommand["Command送信<br/>Start / Cancel / Publish"]
    UIQuery["Read表示<br/>Executions一覧・詳細・Graph"]
  end

  subgraph API["Core-API境界 (Integration/Application)"]
    ExecutionService["ExecutionService<br/>ユースケース統合"]
    ProjectionSync["ExecutionOperationalProjectionSync<br/>投影同期"]
    EngineCallback["Engine観測コールバック<br/>（目標）"]
    ProjectionQueue["API内 Projection キュー<br/>（目標）"]
    DefinitionService["DefinitionService<br/>定義版管理"]
    ApiContract["HTTP契約<br/>/v1/definitions /v1/executions"]
  end

  subgraph Engine["Core-Engine境界 (Domain Kernel)"]
    DefinitionModel["Definition<br/>AST / Compiler"]
    ExecutionModel["Execution<br/>State遷移 / Fact処理"]
    GraphModel["ExecutionGraph<br/>実行グラフ観測"]
    WaitModel["EventWait（durable wait概念）"]
  end

  subgraph DB["永続境界 (PostgreSQL via EF Core)"]
    DefinitionTables["definitions / definition_versions<br/>定義版の正本"]
    ExecutionTables["executions / execution_graph_snapshots<br/>Read Model正本"]
    CursorTables["execution_cursors / execution_waits<br/>Operational Projection"]
    EventStore["event_store<br/>外部可視イベント履歴"]
    Dedup["command_dedup / event_delivery_dedup<br/>冪等・再送制御"]
  end

  UICommand --> ApiContract
  UIQuery --> ApiContract

  ApiContract --> ExecutionService
  ApiContract --> DefinitionService
  ExecutionService --> ExecutionModel
  ExecutionService --> GraphModel
  ExecutionService --> ProjectionSync
  DefinitionService --> DefinitionModel

  DefinitionService --> DefinitionTables
  ExecutionService --> ExecutionTables
  ProjectionSync --> ExecutionTables
  ProjectionSync --> CursorTables
  ExecutionService --> EventStore
  ExecutionService --> Dedup

  ExecutionModel --> WaitModel
  ExecutionModel -.状態変化通知（目標）.-> EngineCallback
  EngineCallback --> ProjectionQueue
  ProjectionQueue --> ProjectionSync

  class DefinitionModel,ExecutionModel,GraphModel,WaitModel domain;
  class ExecutionService,ProjectionSync,EngineCallback,ProjectionQueue,DefinitionService,ApiContract integration;
  class DefinitionTables,ExecutionTables,CursorTables,EventStore,Dedup storage;
  class UICommand,UIQuery external;
```

## 2. 読み方（要点）

- `Core-Engine` は純粋ドメインロジック（定義解釈、遷移、グラフ生成）を担当し、I/O は持たない。
- `Core-API` はユースケース実行とトランザクション境界を担当し、Engine 結果を永続化モデルへ写像する。
- UI の正本は `GET /v1/executions*` が返す Read Model（`executions` / `execution_graph_snapshots`）である。
- `execution_cursors` / `execution_waits` は運用用投影であり、Read API 正本とは分離される。
- `event_store` は外部可視イベント履歴、`command_dedup` / `event_delivery_dedup` は冪等制御の責務を持つ。

## 3. 現状と目標の切り分け

- **現状（実装済み）**: 永続化テーブル更新は `Core-API` からのみ実行される。`UI` や `Core-Engine` が DB に直接書き込む経路は持たない。
- **目標（仕様記載あり）**: Engine の状態変化を API 側へ観測コールバックし、API 内キューで execution 単位に併合しつつ `ProjectionSync` で DB 反映する流れを想定する。
- **図の凡例**: 破線は「目標/導入予定」の経路、実線は「現状の責務境界で成立している経路」を示す。

## 4. 参照元ドキュメント

- `docs/statevia-architecture.md`
- `docs/statevia-data-integration-contract.md`
- `docs/core-api-interface.md`
- `.spec-workflow/specs/execution-platform-data-model/design.md`
