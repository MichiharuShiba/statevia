# Statevia Architecture

Statevia は **Workflow Runtime Platform** であり、以下の3層構造で構成される。

```text
UI → Core-API → Core-Engine
```

## Architecture Overview

```text

+-------------------+
| UI                |
| Workflow Editor   |
| Graph Viewer      |
+---------+---------+
        |
        | HTTP / WebSocket
        v
+---------+---------+
| Core-API          |
| REST Controllers  |
| Auth / Persistence|
+---------+---------+
        |
        | In-process call
        v
+---------+---------+
| Core-Engine       |
| Workflow Runtime  |
| FSM / Scheduler   |
| Join / Execution  |
+---------+---------+
        |
        v
+-------------------+
| Database          |
| Definitions       |
| Workflow Runs     |
| Graph Snapshot    |
+-------------------+

```

---

## Layer Responsibilities

### Core-Engine

Workflow 実行エンジン。

Responsibilities:

* Workflow Runtime
* FSM transitions
* Fork / Join orchestration
* Event waiting
* Parallel scheduling
* ExecutionGraph generation

---

### Core-API

Application layer。

Responsibilities:

* REST API
* Authentication
* Persistence
* Engine orchestration

---

### UI

ユーザー操作と可視化。

Responsibilities:

* Workflow YAML editor
* Graph visualization
* Workflow execution control
* Node inspection

---

### Data Flow

```text

YAML Definition
    ↓
DefinitionLoader
    ↓
DefinitionValidator
    ↓
DefinitionCompiler
    ↓
CompiledWorkflowDefinition
    ↓
WorkflowEngine
    ↓
ExecutionGraph
    ↓
Core-API
    ↓
UI Graph

```
