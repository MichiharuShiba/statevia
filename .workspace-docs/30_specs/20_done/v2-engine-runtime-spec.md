# Engine Runtime 仕様

このドキュメントは Statevia Workflow Engine の
**ランタイム実行仕様（Runtime Semantics）**を定義する。

ここでは以下を定義する。

- Workflow の実行ライフサイクル
- State の実行順序
- Fork / Join の挙動
- Event / Wait の処理
- Scheduler の保証
- Cancel / Failure の扱い

この仕様は Core-Engine の実装が従うべき
**正しい実行ルール**を規定する。

---

## 1. ランタイムモデル

Workflow 実行は WorkflowInstance により管理される。

WorkflowInstance は以下の要素を持つ。

WorkflowInstance
├ ActiveStates
├ ExecutionGraph
├ Outputs
├ FSM
├ JoinTracker
└ Status

各 Workflow は独立して実行される。

---

## 2. Workflow ライフサイクル

Workflow の状態遷移。

Created
↓
Running
↓
Completed | Failed | Cancelled

Workflow は Running 中に
複数の State を並列実行できる。

---

## 3. Workflow 開始

Workflow 開始時の処理。

1. WorkflowInstance を生成
2. InitialState をスケジュール
3. ExecutionGraph ノードを作成
4. State 実行開始

疑似フロー

Start(definition)
  ↓
Create WorkflowInstance
  ↓
ScheduleState(initialState)

---

## 4. State 実行フロー

State 実行は以下の順序で行われる。

ScheduleState
  ↓
StateExecutor 解決
  ↓
ExecutionGraph ノード作成
  ↓
Scheduler 実行
  ↓
Fact 生成
  ↓
ProcessFact

---

## 5. Fact

Fact は State 実行結果を表す。

利用可能な Fact

Completed
Failed
Cancelled
Joined

Fact は FSM の遷移評価に使用される。

---

## 6. 遷移評価 (FSM)

遷移は FSM により評価される。

(stateName, fact)
    ↓
TransitionResult

TransitionResult は以下を含む。

Next
Fork[]
End

---

## 7. Next 遷移

Next は単一の次 State をスケジュールする。

例

A --Completed--> B

実行

ScheduleState(B)

---

## 8. Fork 遷移

Fork は複数の State を並列に実行する。

例

A --Completed--> Fork[B, C]

実行

ScheduleState(B)
ScheduleState(C)

---

## 9. Join 処理

Join は Fork による分岐を合流する。

JoinTracker が各 State の Fact を収集する。

例

Fork → B, C

Join 条件

Join.allOf = [B, C]

すべて完了すると

Joined Fact が生成される。

---

## 10. Join 実行

Join 実行フロー

1. JoinTracker が入力を収集
2. ExecutionGraph ノード作成
3. Fact = Joined
4. FSM 遷移評価

---

## 11. Event 処理

Event は外部からのシグナルである。

例

Wait State

wait:
  event: Approve

実行

State 実行
↓
Event 待機
↓
イベント受信
↓
FSM 遷移

イベントフロー

API → EventProvider → WorkflowInstance

---

## 12. Wait State の意味

Wait State は外部イベントが来るまで
Workflow 実行を停止する。

フロー

State スケジュール
↓
イベント購読
↓
実行停止
↓
イベント受信
↓
遷移評価

---

## 13. Cancel

Workflow は Cancel により停止する。

API

CancelAsync(workflowId)

挙動

1. WorkflowInstance を Cancel 状態にする
2. 実行中 State に CancellationToken を送る
3. 新しい State スケジュールを停止する

Cancel 優先順位

Cancelled > Failed > Completed

---

## 14. Failure

State 実行中に例外が発生した場合

Fact = Failed

処理

Workflow を Failed 状態にする

将来拡張

Retry
Fallback State

---

## 15. Scheduler 仕様

Scheduler は並列実行を管理する。

特徴

- 並列実行可能
- 最大並列数 configurable

DefaultScheduler

ExecutionLimiter(maxParallelism)

---

## 16. ExecutionGraph 更新

ExecutionGraph は実行履歴を保持する。

更新イベント

NodeStarted
NodeCompleted
NodeFailed
NodeCancelled
JoinTriggered

Graph 操作

AddNode
AddEdge
CompleteNode

---

## 17. 実行順序保証

Engine は以下を保証する。

同一 Branch 内の State 実行順は deterministic

並列 Branch は任意順で実行される

Join はすべての入力完了後に評価される

---

## 18. 冪等性

State 実行は冪等であることが望ましい。

将来 Engine は
再実行やリトライを行う可能性がある。

---

## 19. Determinism

Workflow 実行は以下が同じであれば
同じ結果になるべきである。

Workflow 定義
入力
イベント

---

## 20. Runtime Invariants

Engine は以下を保証する。

1. 各 State 実行は必ず 1 ノード生成する
2. Join は一度だけ発火する
3. End 遷移は Workflow を終了する
4. Cancel は新しい State スケジュールを停止する
5. ExecutionGraph は DAG である

---

## 21. 実行例

Workflow

Start
↓
A
↓
Fork
↓
B   C
↓   ↓
Join
↓
End

実行順

A completed
↓
Fork B, C
↓
B completed
↓
C completed
↓
Join triggered
↓
End

---

## 22. Engine 保証

Statevia Engine は以下を保証する。

- 正しい Fork / Join 処理
- Deterministic な遷移評価
- 一貫した ExecutionGraph
- 安全な並列スケジューリング

---

## 23. 将来拡張

将来追加可能な Runtime 機能

Retry Policy
Timeout
Compensation
SubWorkflow
Distributed Execution
