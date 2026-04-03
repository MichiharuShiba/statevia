# Statevia Architecture v1

Statevia は Workflow Runtime Platform である。

Statevia のアーキテクチャは、以下の **3 層構造**で構成される。

UI  
↓  
Core-API  
↓  
Core-Engine

この分離により

- Engine の再利用性
- API の拡張性
- UI の独立開発

を実現する。

---

## 1. アーキテクチャ概要

```text

UI
│
│ HTTP / WebSocket
│
▼
Core-API
│
│ Engine Interface
│
▼
Core-Engine
│
│ Persistence
│
▼
Database

```

---

## 2. レイヤー構成

### 2.1 Core Engine

Workflow 実行エンジン。

Statevia の中核となるコンポーネントであり、
Workflow の実行、遷移、並列処理を管理する。

#### 主な責務

- State 実行
- FSM 遷移評価
- Fork / Join 制御
- Event 待機
- Cancel 処理
- ExecutionGraph 生成

#### 主なコンポーネント

WorkflowEngine  
WorkflowInstance  
TransitionTable (FSM)  
JoinTracker  
Scheduler  
StateExecutor  
ExecutionGraph

---

### 2.2 Core API

Core Engine を外部に公開する **アプリケーション層**。

#### 主な責務

- REST API 提供
- 認証 / 認可
- Workflow 永続化
- Engine 呼び出し

#### 主なコンポーネント

Controllers  
Services  
Repositories

---

### 2.3 UI

Workflow を操作・可視化するユーザーインターフェース。

#### 主な機能

Workflow 定義編集  
Workflow 実行操作  
ExecutionGraph 可視化  
ノード詳細表示

#### 主なコンポーネント

Workflow Editor  
Workflow Runner  
Graph Viewer  
Node Inspector

---

## 3. Runtime フロー

Workflow 実行の基本フロー。

```text

YAML 定義
↓
DefinitionLoader
↓
DefinitionValidator
↓
DefinitionCompiler
↓
CompiledWorkflowDefinition
↓
WorkflowEngine.Start()
↓
WorkflowInstance
↓
ExecutionGraph
↓
Core-API
↓
UI

```

---

## 4. 実行モデル

Statevia の Workflow 実行モデル。

```text

State
↓
Fact
↓
FSM
↓
Next / Fork
↓
Join
↓
Next

```

State の実行結果は **Fact** として評価され、
FSM が次の遷移を決定する。

---

## 5. 主要概念

### 5.1 Fact Driven Execution

State 実行結果は Fact として扱われる。

利用可能な Fact

Completed  
Failed  
Cancelled  
Joined

Fact により Workflow 遷移が決定される。

---

### 5.2 Fork / Join

Workflow の並列処理は明示的に表現される。

Fork

並列 Branch を生成する。

Join

Branch の合流を表す。

---

### 5.3 ExecutionGraph

ExecutionGraph は Workflow の実行履歴を表す。

#### ノード

State 実行

#### エッジ

Next  
Fork  
Join

ExecutionGraph は UI により可視化される。

---

## 6. 設計原則

Statevia アーキテクチャは以下の原則に基づく。

宣言的 Workflow 定義

決定論的実行

明示的な並列制御

実行状態の可視化

---

## 7. 将来のアーキテクチャ拡張

将来的に以下の拡張が可能。

分散 Scheduler

Worker ノード

イベントストリーミング

Workflow Observability

分散 Workflow 実行
