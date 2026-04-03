# Playground 最小 UI 画面仕様（ワイヤーフレーム + 状態遷移）

目的：YAML を書いて即実行し、ExecutionGraph を見ながら Event/Cancel で操作できる最小の Playground を定義する。

---

## 1. 画面一覧（MVP）

- Playground（単一ページ）
  - Editor（YAML）
  - Graph（ExecutionGraph）
  - Runner（Start/Cancel/Event）
  - Inspector（ノード詳細）

---

## 2. ワイヤーフレーム（1ページ）

```text

+----------------------------------------------------------------------------------+
| Toolbar                                                                          |
| [Validate] [Save] [Start] [Cancel]   WorkflowId: (---)   Status: (Idle/Running) |
+-------------------------------+--------------------------------------------------+
| Workflow Editor (YAML)        | Execution Graph Viewer                           |
| - Monaco                      | - DAG Layout                                     |
| - Error underlines            | - Fork/Join grouping                             |
| - Auto format (optional)      | - Wait/Cancel emphasis                           |
|                               |                                                  |
| [ValidationPanel]             |                                                  |
|  - errors/warnings            |                                                  |
+-------------------------------+----------------------------+---------------------+
| Runner Panel                  | Node Inspector             | Event Sender        |
| - Definition: (name/id)       | - state name               | - event name        |
| - Input(optional)             | - status                   | - payload(optional) |
| - Start/Cancel                | - fact                     | [Send]              |
| - Polling indicator           | - output (json)            |                     |
+------------------------------+-----------------------------+---------------------+

```

---

## 3. UI コンポーネント仕様（最小）

### 3.1 Toolbar（上部固定）

表示：

- Validate ボタン
- Save ボタン
- Start ボタン
- Cancel ボタン（Running のときのみ有効）
- WorkflowId 表示（存在する場合）
- Status バッジ（Idle / Running / Completed / Failed / Cancelled）
- Sync 表示（Polling中 / Disconnected）

操作：

- Validate → `POST /definitions (dry-run)` 相当 or `POST /definitions/validate`
- Save → `POST /definitions`
- Start → `POST /workflows`
- Cancel → `POST /workflows/{id}/cancel`

---

### 3.2 Workflow Editor（左上）

入力：

- YAML テキスト

表示：

- YAML を編集できる
- バリデーションエラーの行表示（最小は ValidationPanel にテキストでもOK）

イベント：

- onChange → local state 更新
- Validate/Save 成功 → errors クリア、definitionId 保存

---

### 3.3 ValidationPanel（左下）

表示：

- errors[]（必須）
- warnings[]（任意）

ルール：

- errors が1つでもあれば Start は無効（MVP推奨）

---

### 3.4 Execution Graph Viewer（右上）

入力：

- ExecutionGraph（nodes/edges）

表示ルール（MVP最低限）：

- Fork/Join を “まとまり” に見せる（枝を近接配置）
- Wait（Waiting）を強調表示
- Cancelled / Failed を強調表示
- Running は控えめ（薄いハイライト）

操作：

- node click → selectedNodeId 更新 → Inspector に反映

更新：

- Running 中は polling で `GET /workflows/{id}/graph` を一定間隔で更新

---

### 3.5 Runner Panel（左下帯）

表示：

- definitionId / definitionName
- workflowId（開始後）
- status
- polling 状態

操作：

- Start / Cancel（Toolbar と同等でも良いが、下にも置くと使いやすい）

---

### 3.6 Node Inspector（右下中央）

入力：

- selectedNodeId
- graph.nodes

表示：

- state
- status
- fact
- startedAt / completedAt（あれば）
- output（JSON）

---

### 3.7 Event Sender（右下右端）

入力：

- eventName
- payload（optional json）

操作：

- Send → `POST /workflows/{id}/events`

ルール：

- workflowId がない場合は無効
- workflow status が Running のときのみ有効（MVP推奨）
- Cancelled の場合は無効（Cancel勝ち）

---

## 4. 状態遷移（UI State Machine）

UI の状態は最小で以下を持つ。

- editorState
- definitionState
- runState
- graphState
- selectionState

---

### 4.1 Editor State

States:

- Dirty（編集中）
- Validating
- Valid（エラーなし）
- Invalid（エラーあり）
- Saving

Transitions:

- edit → Dirty
- Validate click → Validating → (Valid | Invalid)
- Save click → Saving → (Valid | Invalid)

Rules:

- Invalid の間は Start 無効（推奨）

---

### 4.2 Definition State

States:

- None（未保存）
- Saved（definitionIdあり）

Transitions:

- Save success → Saved
- Edit after save → Saved のままでも良い（ただし Dirty を持つ）

---

### 4.3 Run State

States:

- Idle（workflowIdなし）
- Starting
- Running
- Completed
- Failed
- Cancelled

Transitions:

- Start click → Starting → Running
- Poll snapshot: status change → Completed/Failed/Cancelled
- Cancel click → CancelRequested → Cancelled（または Running のまま少し後に Cancelled）

Rules:

- Start は Saved && Valid のときのみ可能（MVP推奨）
- Running 中に Start を押せない
- Cancel は Running 中のみ可能

---

### 4.4 Graph State

States:

- Empty（workflowIdなし）
- Loading（初回取得）
- Ready（表示中）
- Error（取得失敗）

Transitions:

- workflowId set → Loading → Ready
- polling update → Ready（差分更新）
- fetch error → Error（復帰可能）

---

### 4.5 Selection State

States:

- None
- NodeSelected(nodeId)

Transitions:

- click node → NodeSelected
- click blank → None
- graph update で node が消えた → None

---

## 5. Polling 仕様（MVP）

目的：リアルタイム更新の代替。

Interval:

- 1000ms〜2000ms

Requests:

- GET /workflows/{id}      （snapshot）
- GET /workflows/{id}/graph（graph）

Stop:

- runState が Completed/Failed/Cancelled になったら polling 停止（または低頻度化）

---

## 6. 最小 API 依存（UI が必要なもの）

- POST /definitions
- POST /workflows
- POST /workflows/{id}/events
- POST /workflows/{id}/cancel
- GET /workflows/{id}
- GET /workflows/{id}/graph

---

## 7. MVP Done Criteria（完成条件）

- YAML を書いて Validate できる
- Save して definitionId が得られる
- Start で workflowId が得られる
- Graph が表示され、実行状態が更新される
- Wait ノードに Event を送れる
- Cancel で停止できる
- ノードをクリックすると Inspector が更新される

---

## 8. 重要な表示ポリシー（Stateviaらしさ）

- Fork/Join：構造としてまとまり
- Wait/Resume/Cancel：操作点として強調
- Running：控えめ
- Failed/Cancelled：一目で分かる
