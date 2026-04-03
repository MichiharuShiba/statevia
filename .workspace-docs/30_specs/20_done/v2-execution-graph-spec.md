# Execution Graph Specification

ExecutionGraph は Workflow 実行を **視覚化するための構造データ**である。

Core-Engine は ExecutionGraph を生成し、
UI はそれを描画する。

ExecutionGraph は **状態遷移ログではなく、実行構造の可視化モデル**である。

---

## 1. Graph Structure

ExecutionGraph は以下の構造を持つ。

```text

Graph
├ nodes[]
└ edges[]

```

---

## 2. Node

Node は Workflow の State 実行を表す。

```text

{
"id": "n1",
"state": "ApproveOrder",
"status": "Completed",
"fact": "Completed",
"startedAt": "2026-02-20T12:00:00Z",
"completedAt": "2026-02-20T12:00:02Z",
"output": {}
}

```

---

### Node Fields

| field       | description                              |
| ----------- | ---------------------------------------- |
| id          | node identifier                          |
| state       | state name                               |
| status      | Running / Completed / Failed / Cancelled |
| fact        | execution result                         |
| startedAt   | execution start                          |
| completedAt | execution end                            |
| output      | state output                             |

---

## 3. Edge

Edge は State 間の関係を表す。

```text

{
"from": "n1",
"to": "n2",
"type": "Next"
}

```

---

### Edge Types

| type | description       |
| ---- | ----------------- |
| Next | normal transition |
| Fork | parallel branch   |
| Join | branch merge      |

---

## 4. Graph Example

```text

Start
  |
  v
  A
  |
Fork
 / \
 v v
 B C
 \ /
Join
  |
  v
  D
  |
 End

```

Graph JSON

```text

{
    "nodes": [
        {"id":"n1","state":"A","status":"Completed"},
        {"id":"n2","state":"B","status":"Completed"},
        {"id":"n3","state":"C","status":"Running"},
        {"id":"n4","state":"D","status":"Pending"}
    ],
    "edges":[
        {"from":"n1","to":"n2","type":"Fork"},
        {"from":"n1","to":"n3","type":"Fork"},
        {"from":"n2","to":"n4","type":"Join"},
        {"from":"n3","to":"n4","type":"Join"}
    ]
}

```

---

## 5. Node Status

| status    | meaning      |
| --------- | ------------ |
| Pending   | not executed |
| Running   | executing    |
| Completed | success      |
| Failed    | error        |
| Cancelled | cancelled    |

---

## 6. Visual Rules

ExecutionGraph の UI 表示ルール。

---

### 6.1 Fork

Fork は **視覚的にまとまりを持つ**。

```text

  A
  |
 Fork
 / \
 B  C

```

UI:

- Fork 起点は横分岐
- Branch は並列表示

---

### 6.2 Join

Join は **branch merge** を表す。

```text

B C
\ /
Join
 |
 D

```

UI:

- Join ノードに branch を収束
- Join は diamond または merge marker

---

### 6.3 Wait

Wait state は **ユーザー操作待ち**。

UI 表現:

- 時計アイコン
- pulse animation

```text

     A
     |
WaitApproval

```

---

### 6.4 Resume

Resume は **外部イベントで再開**。

UI 表現:

- event marker
- resume animation

---

### 6.5 Cancel

Cancel は **強いイベント**。

UI 表現:

- 赤ライン
- workflow 全体停止

---

### 6.6 Running

Running state は **控えめ表示**。

理由:

実行中は transient 状態。

UI:

- subtle animation
- highlight minimal

---

### 6.7 Failed

Failed は **強調表示**。

UI:

- 赤
- error icon

---

## 7. Layout Rules

Graph layout は DAG。

ルール:

```text

Start
  ↓
State
  ↓
Fork
  ↓
Parallel
  ↓
Join
  ↓
Next

```

Layout engine:

- dagre
- elk
- d3

推奨: **dagre**

---

## 8. Incremental Updates

Graph はリアルタイム更新可能。

更新イベント:

```text

NodeStarted
NodeCompleted
NodeFailed
NodeCancelled
JoinTriggered

```

UI は差分更新する。

---

## 9. Graph Export API

Core-API:

```text

GET /workflows/{id}/graph

```

Response

```text

{
    "nodes": [],
    "edges": []
}

```

---

## 10. Design Principles

ExecutionGraph UI は以下を重視する。

1. Workflow の **構造理解**
2. 実行状態の **即時把握**
3. Fork / Join の **視覚的理解**
4. Wait / Resume / Cancel の **ユーザー操作理解**

---

## 11. Example UI

```text

    [Start]
       |
    [A Completed]
       |
    [Fork]
     /         \
[B Completed] [C Running]
     \         /
    [Join]
       |
    [D Pending]

```
