# Statevia System Diagram

Statevia は Workflow Runtime Platform である。

ユーザーは Workflow を宣言的に定義し、
Statevia Engine がそれを実行する。

---

## System Overview

```text
      +----------------------+
      |        UI            |
      |----------------------|
      | Workflow Editor      |
      | Workflow Runner      |
      | Execution Graph View |
      +----------+-----------+
                 |
                 | HTTP / WebSocket
                 |
                 v
      +----------+-----------+
      |       Core API       |
      |----------------------|
      | REST Controllers     |
      | Auth / Validation    |
      | Workflow Management  |
      +----------+-----------+
                 |
                 | Engine Interface
                 |
                 v
      +----------+-----------+
      |      Core Engine     |
      |----------------------|
      | WorkflowEngine       |
      | FSM (TransitionTable)|
      | JoinTracker          |
      | Scheduler            |
      | StateExecutor        |
      | ExecutionGraph       |
      +----------+-----------+
                 |
                 | Persistence
                 |
                 v
      +----------+-----------+
      |        Database      |
      |----------------------|
      | WorkflowDefinitions  |
      | WorkflowRuns         |
      | ExecutionGraph       |
      | WorkflowEvents       |
      +----------------------+
```

---

## Workflow Execution Flow

Workflow 実行の流れ。

```text

YAML Workflow Definition
│
▼
DefinitionLoader
│
▼
DefinitionValidator
│
▼
DefinitionCompiler
│
▼
CompiledWorkflowDefinition
│
▼
WorkflowEngine.Start()
│
▼
WorkflowInstance
│
▼
State Execution
│
▼
Facts Generated
│
▼
FSM Transition
│
▼
Next / Fork
│
▼
Join Evaluation
│
▼
ExecutionGraph Updated
│
▼
Core API
│
▼
UI Graph Visualization

```

---

## Execution Model

Statevia は **Fact Driven Workflow Engine** である。

```text

State Execution
│
▼
Fact Produced
│
▼
FSM Evaluation
│
▼
Transition
│
├─ Next
│
├─ Fork (Parallel Execution)
│
└─ End

```

---

## Parallel Execution

Fork / Join による並列実行。

```text
    A
    │
   Fork
  /    \
 B      C
  \    /
   Join
    │
    D
```

---

## Execution Graph

Workflow の実行状態は ExecutionGraph として記録される。

```text

Nodes
State executions

Edges
Next
Fork
Join

```

ExecutionGraph は UI によりリアルタイム可視化される。

---

## Design Goals

Statevia の設計目標。

- 宣言的 Workflow 定義
- Deterministic Execution
- 明示的な並列処理
- 実行状態の可視化
- Developer Friendly DSL

---

## Future Architecture

将来的な拡張。

```text

Worker Nodes
Distributed Scheduler
Event Streaming
Observability
Multi-tenant SaaS

```
