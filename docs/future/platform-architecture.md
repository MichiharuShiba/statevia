# プラットフォームアーキテクチャ（将来構想）

| 項目 | 値 |
| --- | --- |
| 種別 | Future |
| Version | 1.2.0 |
| 更新日 | 2026-07-07 |
| 関連 | [architecture/overview.md](../architecture/overview.md), [architecture/repository-layout.md](../architecture/repository-layout.md) |

---

**現時点では未実装の構想**です。現行の実装・契約は [architecture/](../architecture/) および [specifications/](../specifications/) を参照すること。

本書は、論理プラットフォーム構成の将来到達像を示す。

---

## プラットフォーム構成（論理）

Definition / Execution / Projection / Realtime / Action Runtime / Persistence を中心とした論理ブロック図。ノード背景は白、サブグラフ背景は Mermaid 既定。枠線色はブロック種別、矢印色はフロー種別に対応する。

```mermaid
flowchart TB

  classDef client fill:#ffffff,stroke:#475569,stroke-width:1px,color:#0f172a;
  classDef api fill:#ffffff,stroke:#2563eb,stroke-width:2px,color:#1e3a8a;
  classDef platform fill:#ffffff,stroke:#4f46e5,stroke-width:1px,color:#312e81;
  classDef action fill:#ffffff,stroke:#a855f7,stroke-width:1px,color:#581c87;
  classDef storage fill:#ffffff,stroke:#ea580c,stroke-width:1px,color:#431407;
  classDef infra fill:#ffffff,stroke:#16a34a,stroke-width:1px,color:#14532d;
  classDef observability fill:#ffffff,stroke:#ca8a04,stroke-width:1px,color:#713f12;

  subgraph Clients["Clients"]
    direction LR
    Browser["Web UI"]
    CLI["CLI / SDK"]
    External["External Systems"]
  end

  subgraph API["Platform API"]
    PlatformAPI["Platform API<br/>Auth · Authorization · Tenants<br/>Definitions · Executions · Events"]
  end

  subgraph Platform["Statevia Platform"]

    subgraph Definition["Definition Platform"]
      direction LR
      Schema["Schema"]
      Validate["Validation"]
      Compile["Compilation"]
      Version["Versioning"]
      Package["Packaging"]
      Sign["Signing"]
    end

    subgraph Execution["Execution Platform"]
      direction TB
      Dispatcher["Dispatcher"]
      Runtime["Execution Runtime"]
      subgraph ExecCaps["Capabilities"]
        direction LR
        FSM["FSM"]
        Wait["Wait / Resume"]
        Fork["Fork / Join"]
        Retry["Retry / Cancel"]
        Recovery["Recovery"]
      end
    end

    subgraph Projection["Projection Platform"]
      direction TB
      ProjectionWorker["Projection"]
      ReadModel["Read Models"]
      Timeline["Timeline"]
      Graph["Graph View"]
    end

    subgraph Realtime["Realtime Platform"]
      Push["SSE / WebSocket"]
    end

  end

  subgraph Actions["Action Runtime"]
    direction TB
    Runner["Action Runner"]
    subgraph ActionKinds[" "]
      direction LR
      Builtin["Built-in Actions"]
      Modules["Action Modules"]
      Connectors["Connectors"]
    end
  end

  subgraph Storage["Persistence"]
    direction LR
    DefinitionStore["Definition Store"]
    EventStore["Event Store"]
    SnapshotStore["Snapshot Store"]
    ArtifactStore["Artifact Store"]
    ReadStore["Read Model Store"]
  end

  subgraph Infra["Infrastructure"]
    direction LR
    Transport["Transport<br/>In-Memory · Kafka<br/>RabbitMQ · Azure SB"]
    Secrets["Secrets / KMS"]
  end

  subgraph Observability["Observability"]
    direction LR
    Logs["Logs"]
    Metrics["Metrics"]
    Tracing["Tracing"]
    Audit["Audit"]
  end

  %% --- Ingress ---
  Browser --> PlatformAPI
  CLI --> PlatformAPI
  External --> PlatformAPI

  %% --- Platform API → subsystems ---
  PlatformAPI --> Definition
  PlatformAPI --> Execution
  PlatformAPI --> Projection
  PlatformAPI --> Realtime

  %% --- Persistence writes ---
  Definition --> DefinitionStore
  Execution --> EventStore
  Execution --> SnapshotStore
  Execution --> ArtifactStore
  Execution --> ProjectionWorker
  ProjectionWorker --> ReadStore
  Realtime --> ReadStore

  %% --- Action execution ---
  Execution --> Runner
  Runner --> Builtin
  Runner --> Modules
  Runner --> Connectors
  Runner --> Secrets

  %% --- Async dispatch ---
  Execution -. &nbsp;&nbsp;Dispatch&nbsp;&nbsp; .-> Transport
  Runner -. &nbsp;&nbsp;Dispatch&nbsp;&nbsp; .-> Transport

  %% --- Observability (logs) ---
  Definition --> Logs
  Execution --> Logs
  Projection --> Logs
  Realtime --> Logs
  Runner --> Logs

  %% --- Observability (metrics) ---
  Definition --> Metrics
  Execution --> Metrics
  Projection --> Metrics
  Realtime --> Metrics
  Runner --> Metrics

  %% --- Observability (audit) ---
  Definition --> Audit
  Execution --> Audit
  Projection --> Audit

  class Browser,CLI,External client;
  class PlatformAPI api;
  class Schema,Validate,Compile,Version,Package,Sign,Dispatcher,Runtime,FSM,Wait,Fork,Retry,Recovery,ProjectionWorker,ReadModel,Timeline,Graph,Push platform;
  class Runner,Builtin,Modules,Connectors action;
  class DefinitionStore,EventStore,SnapshotStore,ArtifactStore,ReadStore storage;
  class Transport,Secrets infra;
  class Logs,Metrics,Tracing,Audit observability;

  style Platform stroke:#6366f1,stroke-width:2px;
  style Clients stroke:#94a3b8;
  style API stroke:#2563eb,stroke-width:2px;
  style Actions stroke:#a855f7;
  style Storage stroke:#ea580c;
  style Infra stroke:#16a34a;
  style Observability stroke:#ca8a04;

  linkStyle 0,1,2 stroke:#475569,stroke-width:2px;
  linkStyle 3,4,5,6 stroke:#2563eb,stroke-width:2px;
  linkStyle 7,8,9,10,11,12,13 stroke:#ea580c,stroke-width:2px;
  linkStyle 14,15,16,17 stroke:#a855f7,stroke-width:2px;
  linkStyle 18 stroke:#16a34a,stroke-width:2px;
  linkStyle 19,20 stroke:#16a34a,stroke-width:2px,stroke-dasharray:6;
  linkStyle 21,22,23,24,25 stroke:#ca8a04,stroke-width:1px;
  linkStyle 26,27,28,29,30 stroke:#ca8a04,stroke-width:1px;
  linkStyle 31,32,33 stroke:#ca8a04,stroke-width:1px;
```
