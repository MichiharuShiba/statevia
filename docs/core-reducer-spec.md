# Core Reducer Specification (Pseudo Code)

Version: 1.0
Project: 実行型ステートマシン
Policy: Cancel wins

---

## 1. ゴール

- Event を適用して ExecutionState を更新する **純粋 reducer** を定義する
- 競合（Cancel vs Complete/Fail/Resume 等）は **散発的if** ではなく、
  **共通の優先順位関数**で機械的に解決する

---

## 2. 状態モデル（最小）

```pseudo
enum ExecutionStatus { ACTIVE, COMPLETED, FAILED, CANCELED }
enum NodeStatus { IDLE, READY, RUNNING, WAITING, SUCCEEDED, FAILED, CANCELED }

struct NodeState {
  nodeId: string
  nodeType: string
  status: NodeStatus
  attempt: int
  workerId?: string
  waitKey?: string
  output?: object
  error?: object

  // 監査/説明性用（推奨）
  canceledByExecution?: bool   // Execution cancel の影響で実質キャンセル扱いになった
}

struct ExecutionState {
  executionId: string
  graphId: string
  status: ExecutionStatus
  nodes: Map<string, NodeState>

  cancelRequestedAt?: timestamp
  canceledAt?: timestamp

  failedAt?: timestamp
  completedAt?: timestamp

  version: int  // optimistic lock
}
````

---

## 3. 優先順位関数（共通・必須）

### 3.1 ExecutionStatus の優先順位

```pseudo
function execRank(s: ExecutionStatus): int =
  switch s:
    case CANCELED:  400
    case FAILED:    300
    case COMPLETED: 200
    case ACTIVE:    100

function chooseExecStatus(current, candidate): ExecutionStatus =
  return (execRank(candidate) > execRank(current)) ? candidate : current
```

### 3.2 NodeStatus の優先順位

```pseudo
function nodeRank(s: NodeStatus): int =
  switch s:
    case CANCELED:  700
    case FAILED:    600
    case SUCCEEDED: 500
    case WAITING:   400
    case RUNNING:   300
    case READY:     200
    case IDLE:      100

function chooseNodeStatus(current, candidate): NodeStatus =
  return (nodeRank(candidate) > nodeRank(current)) ? candidate : current
```

> **Cancel wins は rank で担保**する。
> reducer 内で「Cancelだけ特別扱いのif」を増やさない。

---

## 4. ガード関数（共通）

### 4.1 終端チェック

```pseudo
function isTerminalExec(s: ExecutionStatus): bool =
  return s in { COMPLETED, FAILED, CANCELED }
```

### 4.2 Cancelが「要求済み」か

```pseudo
function isCancelRequested(state: ExecutionState): bool =
  return state.cancelRequestedAt != null
```

### 4.3 Cancel要求以降の “進行系イベント” を抑止

Cancel wins を UX と整合させるため、**要求以降は進行系を No-op 推奨**。
（ただし監査のため Event 自体は残る運用でも reducer は安全に動く）

```pseudo
function shouldIgnoreProgressEvent(state, eventType): bool =
  if not isCancelRequested(state): return false
  // 進行を進めるタイプは抑止（必要最小限）
  return eventType in {
    "NODE_READY", "NODE_STARTED", "NODE_PROGRESS_REPORTED",
    "NODE_WAITING", "NODE_RESUME_REQUESTED", "NODE_RESUMED",
    "JOIN_PASSED", "JOIN_GATE_UPDATED", "FORK_OPENED",
    "EXECUTION_COMPLETED", "EXECUTION_FAILED"
  }
```

> ここは “推奨ポリシー” として固定。
> 監査目的で進行系を残したい場合でも、最終状態は Cancel で上書きされないよう chooseExecStatus で守られる。

---

## 5. ルート reducer

```pseudo
function reduce(state: ExecutionState, event: EventEnvelope): ExecutionState =
  if event.schemaVersion != 1:
    return state  // future: versioned reducer

  // (A) Cancel要求以降の進行イベントはNo-op（推奨）
  if shouldIgnoreProgressEvent(state, event.type):
    return state

  // (B) typeごとに apply
  newState = applyEvent(state, event)

  // (C) 最後に整合性（invariants）を強制
  return normalize(newState)
```

---

## 6. applyEvent（type別の適用）

```pseudo
function applyEvent(state, event):
  switch event.type:

    // --- Execution lifecycle ---
    case "EXECUTION_CREATED":
      return state.with(
        graphId = event.payload.graphId,
        status = ACTIVE
      )

    case "EXECUTION_STARTED":
      return state.with(status = chooseExecStatus(state.status, ACTIVE))

    case "EXECUTION_CANCEL_REQUESTED":
      // cancelRequestedAt は一度入ったら保持（最初の時刻を採用）
      if state.cancelRequestedAt == null:
        state = state.with(cancelRequestedAt = event.occurredAt)
      return state

    case "EXECUTION_CANCELED":
      state = state.with(
        canceledAt = state.canceledAt ?? event.occurredAt
      )
      state = state.with(status = chooseExecStatus(state.status, CANCELED))
      return state

    case "EXECUTION_FAILED":
      // Cancel wins: chooseExecStatus が CANCELED を上書きしない
      if state.failedAt == null:
        state = state.with(failedAt = event.occurredAt)
      state = state.with(status = chooseExecStatus(state.status, FAILED))
      return state

    case "EXECUTION_COMPLETED":
      if state.completedAt == null:
        state = state.with(completedAt = event.occurredAt)
      state = state.with(status = chooseExecStatus(state.status, COMPLETED))
      return state

    case "EXECUTION_ARCHIVED":
      return state  // archiveは状態に影響しない（別ストレージなら外）

    // --- Node lifecycle ---
    case "NODE_CREATED":
      nodeId = event.payload.nodeId
      if state.nodes.contains(nodeId): return state
      node = NodeState(
        nodeId=nodeId,
        nodeType=event.payload.nodeType,
        status=IDLE,
        attempt=0
      )
      return state.with(nodes = state.nodes.put(nodeId, node))

    case "NODE_READY":
      return updateNodeStatus(state, event.payload.nodeId, READY)

    case "NODE_STARTED":
      nodeId = event.payload.nodeId
      state = updateNodeStatus(state, nodeId, RUNNING)
      return state.updateNode(nodeId, n =>
        n.with(
          attempt = max(n.attempt, event.payload.attempt),
          workerId = event.payload.workerId ?? n.workerId
        )
      )

    case "NODE_PROGRESS_REPORTED":
      return state  // progressは別フィールドに入れてもよい（必須ではない）

    case "NODE_WAITING":
      nodeId = event.payload.nodeId
      state = updateNodeStatus(state, nodeId, WAITING)
      return state.updateNode(nodeId, n => n.with(waitKey = event.payload.waitKey ?? n.waitKey))

    case "NODE_RESUME_REQUESTED":
      return state  // request自体は監査用。状態は変えない（推奨）

    case "NODE_RESUMED":
      // WAITING -> RUNNING (優先順位で自然に決まる)
      return updateNodeStatus(state, event.payload.nodeId, RUNNING)

    case "NODE_SUCCEEDED":
      nodeId = event.payload.nodeId
      state = updateNodeStatus(state, nodeId, SUCCEEDED)
      return state.updateNode(nodeId, n => n.with(output = event.payload.output ?? n.output))

    case "NODE_FAIL_REPORTED":
      // reportは監査用。必要なら error を保持
      nodeId = event.payload.nodeId
      return state.updateNode(nodeId, n => n.with(error = event.payload.error ?? n.error))

    case "NODE_FAILED":
      nodeId = event.payload.nodeId
      state = updateNodeStatus(state, nodeId, FAILED)
      return state.updateNode(nodeId, n => n.with(error = event.payload.error ?? n.error))

    // --- Node cancellation ---
    case "NODE_CANCEL_REQUESTED":
      return state  // requestは監査用。状態は変えない（推奨）

    case "NODE_INTERRUPT_REQUESTED":
      return state  // worker側の副作用トリガ。状態は変えない（推奨）

    case "NODE_CANCELED":
      nodeId = event.payload.nodeId
      return updateNodeStatus(state, nodeId, CANCELED)

    // --- Graph control (Fork/Join) ---
    case "FORK_OPENED":
      // fork自体は監査。READY化は NODE_READY イベントで表現する（責務分離）
      return state

    case "JOIN_GATE_UPDATED":
      // gate状態を保持するなら別フィールドに格納。必須でなければNo-op。
      return state

    case "JOIN_PASSED":
      return state  // join通過の事実。次のREADY化は NODE_READY で表現。

    default:
      return state
```

---

## 7. ノード更新ヘルパ

```pseudo
function updateNodeStatus(state, nodeId, candidateStatus):
  if not state.nodes.contains(nodeId): return state
  return state.updateNode(nodeId, n =>
    n.with(status = chooseNodeStatus(n.status, candidateStatus))
  )
```

---

## 8. normalize（整合性強制）

### 8.1 Execution が CANCELED 確定なら未終端ノードを収束（推奨）

> ここは “設計選択” があるため、**デフォルト挙動を固定**しておく。
> 実行中/待機中を残すと UI/説明性が崩れるため、コアは収束させる。

```pseudo
function normalize(state):
  if state.status == CANCELED:
    for each node in state.nodes:
      if node.status in { IDLE, READY, RUNNING, WAITING }:
        node.status = chooseNodeStatus(node.status, CANCELED)
        node.canceledByExecution = true
    return state

  // FAILED/COMPLETED は必要なら類似の収束も可能（今回は最小）
  return state
```

---

## 9. Reducer の不変条件（Invariants）

* ExecutionStatus は rank によって単調に強くなる方向にしか変化しない
* NodeStatus も同様
* EXECUTION_CANCELED 以降は ExecutionStatus は固定（chooseExecStatusで保証）
* CancelRequested が一度入ったら消えない

---

## 10. 副作用（Effect）との分離

reducer は副作用を起こさない。
中断要求や次ノードのREADY化などは **Event生成側（Process Manager / Orchestrator）** が担当し、
その結果を Event として reducer に流す。

例:

- Cancel受理 → Orchestrator が NODE_INTERRUPT_REQUESTED を発行
- Join成立 → Orchestrator が 次ノードの NODE_READY を発行
