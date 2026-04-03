# Core Engine Specification

Core Engine は Statevia の **Workflow Runtime** である。

---

## Main Components

```text

WorkflowEngine
├ WorkflowInstance
├ FSM (TransitionTable)
├ JoinTracker
├ Scheduler
├ StateExecutor
├ EventProvider
└ ExecutionGraph

```

---

## WorkflowEngine

Entry point。

Public API:

```text

Start(definition)
PublishEvent(eventName)
CancelAsync(workflowId)
GetSnapshot(workflowId)
ExportExecutionGraph(workflowId)

```

---

## WorkflowInstance

Workflow の実行状態。

Fields:

```text

WorkflowId
Definition
Fsm
JoinTracker
Graph
ActiveStates
Outputs
Status

```

---

## FSM

TransitionTable evaluates:

```text

(stateName, fact) -> TransitionResult

```

TransitionResult:

```text

Next
Fork[]
End

```

---

## Facts

Facts represent state execution results.

```text

Completed
Failed
Cancelled
Joined

```

---

## State Execution

Execution flow:

```text

ScheduleState
↓
Executor.Resolve
↓
Graph.AddNode
↓
Scheduler.Run
↓
Fact
↓
ProcessFact

```

---

## Fork

Fork transitions create parallel executions.

```text

foreach nextState
ScheduleState(nextState)

```

---

## Join

JoinTracker collects incoming state facts.

When all inputs satisfied:

```text

Joined fact emitted

```

---

## ProcessFact

```text

JoinTracker.RecordFact
↓
Join Ready?
↓
RunJoinState

Else

FSM.Evaluate
↓
Next / Fork / End

```

---

## Scheduler

DefaultScheduler executes states with parallelism limit.

```text

ExecutionLimiter(maxParallelism)

```

---

## ExecutionGraph

ExecutionGraph records runtime execution.

```text

AddNode(state)
AddEdge(from,to,type)
CompleteNode(node,fact,output)

```

Edge Types:

```text

Next
Fork
Join

```

---

## Cancellation Rules

Cancel has priority.

```text

Cancelled > Failed > Completed

```

If Cancel occurs:

```text

workflow stops
running states receive cancellation token

```
