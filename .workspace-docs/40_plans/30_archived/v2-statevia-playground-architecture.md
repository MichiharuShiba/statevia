# Statevia Playground Architecture

このドキュメントは Statevia Playground（UI）のアーキテクチャを定義する。

目的：

- Workflow DSL を編集（YAML）
- Workflow を実行（Start / Cancel / Event）
- ExecutionGraph を可視化（Fork/Join を構造として表示）
- 実行状態をデバッグ（ノード詳細、Fact、Output）

Playground は Statevia の価値を体験するための主要プロダクトである。

---

## 1. 全体構成

Playground は 3 層で構成される。

UI (Playground)
↓
Core API
↓
Core Engine

UI は REST / WebSocket により Core API と通信する。

---

## 2. UI 技術スタック（推奨）

- React + TypeScript
- 状態管理：軽量（Zustand or React Query + local state）
- Editor：Monaco Editor（推奨）
- Graph：React Flow（最短） or D3 + dagre（自由度高）
- Layout：dagre（DAGレイアウト）
- UI：Tailwind + shadcn/ui（任意）

最短MVPは **React Flow + dagre** が最も早い。

---

## 3. ディレクトリ構成（推奨）

```text
ui/
└─ src/
   ├─ app/
   │
   ├─ routes/
   │  └─ playground/
   │     └─ PlaygroundPage.tsx
   │
   ├─ components/
   │  ├─ editor/
   │  │  ├─ WorkflowEditor.tsx
   │  │  └─ ValidationPanel.tsx
   │  │
   │  ├─ runner/
   │  │  ├─ RunnerPanel.tsx
   │  │  └─ EventSender.tsx
   │  │
   │  ├─ graph/
   │  │  ├─ GraphView.tsx
   │  │  └─ graphLayout.ts
   │  │
   │  ├─ nodeRenderers/
   │  │  ├─ ActionNode.tsx
   │  │  ├─ WaitNode.tsx
   │  │  └─ JoinNode.tsx
   │  │
   │  └─ inspector/
   │     └─ NodeInspector.tsx
   │
   ├─ common/
   │  ├─ SplitPane.tsx
   │  └─ Toolbar.tsx
   │
   ├─ api/
   │  ├─ client.ts
   │  ├─ definitions.ts
   │  ├─ workflows.ts
   │  └─ graph.ts
   │
   ├─ store/
   │  └─ playgroundStore.ts
   │
   └─ types/
      ├─ dto.ts
      └─ graph.ts

```

---

## 4. 画面レイアウト

```text

+-----------------------------+-----------------------------+
| Workflow Editor (YAML)      | Execution Graph Viewer      |
| - Monaco                    | - ReactFlow + dagre         |
| - Validate/Save buttons     | - Fork/Join grouping        |
+-----------------------------+-----------------------------+
| Runner / Controls           | Node Inspector              |
| - Start / Cancel / Event    | - status, fact, output      |
+-----------------------------+-----------------------------+

```

---

## 5. UI 状態モデル（最小）

UI の状態は以下で管理する。

- currentDefinitionYaml: string
- currentDefinitionId?: string
- currentWorkflowId?: string
- snapshot?: WorkflowSnapshot
- graph?: ExecutionGraph
- selectedNodeId?: string
- connectionStatus: "idle" | "polling" | "ws"

推奨：React Query で snapshot/graph を取得し、UI状態は store で持つ。

---

## 6. API インターフェース（UI から見た仕様）

UI は以下を使用する。

### 6.1 Definition

- POST /definitions
  - request: { name, yaml }
  - response: { definitionId, errors? }

- GET /definitions/{id}

### 6.2 Workflow

- POST /workflows
  - request: { definitionId }
  - response: { workflowId }

- POST /workflows/{id}/cancel

- POST /workflows/{id}/events
  - request: { name, payload? }

- GET /workflows/{id}
  - response: snapshot

- GET /workflows/{id}/graph
  - response: graphJson

---

## 7. リアルタイム更新方式（2段階）

### 7.1 MVP：Polling

MVP は polling で十分。

- 1秒〜2秒間隔で
  - GET /workflows/{id}
  - GET /workflows/{id}/graph

UI は差分を吸収して描画を更新する。

### 7.2 拡張：WebSocket / SSE

将来：

- /workflows/{id}/stream

イベント例：

- NodeStarted
- NodeCompleted
- NodeFailed
- NodeCancelled
- JoinTriggered
- GraphUpdated
- SnapshotUpdated

UI は差分イベントで更新する。

---

## 8. ExecutionGraph → UI Graph 変換

ExecutionGraph API は nodes/edges を返す。

UI はこれを ReactFlow の形式に変換する。

### 8.1 Node Mapping

ExecutionNode → ReactFlowNode

- id: string
- position: layoutで設定
- type: node renderer type
- data:
  - state
  - status
  - fact
  - output
  - timestamps

Node type mapping:

- status と state type で renderer を選択

例：

- wait state → WaitNode
- join state → JoinNode
- action state → ActionNode

### 8.2 Edge Mapping

ExecutionEdge → ReactFlowEdge

- id: `${from}-${to}-${type}`
- source: from
- target: to
- type:
  - NextEdge
  - ForkEdge
  - JoinEdge

---

## 9. Graph Layout（dagre）

Graph は DAG として配置する。

### 9.1 レイアウト戦略

- 上から下（TB）
- Fork は左右に開く
- Join は中央に収束

### 9.2 Fork/Join の “まとまり” を作る

Fork node を起点として、その下に branch 群をまとめる。

UI の要件：

- Fork/Join は視覚的にひとまとまり
- Wait/Resume/Cancel は強調
- Running は抑えめ
- Failed/Cancelled は一目で分かる

---

## 10. UI 表示ルール（重要）

### 10.1 Fork / Join

- Fork edge は分岐を明示
- Join edge は収束を明示
- branch 群は近接して表示

### 10.2 Wait / Resume / Cancel

強調対象。

- Wait node：時計アイコン / 強調枠
- Resume：イベントマーカー（エフェクト）
- Cancel：赤系の強い停止表現

### 10.3 Running

- 変化する状態のため控えめ
- 小さな pulse / shimmer 程度

### 10.4 Failed / Cancelled

- 失敗/キャンセルは最重要シグナル
- 強いコントラスト（色/アイコン/枠）

---

## 11. Node Inspector（デバッグの核）

ノードクリックで右下に詳細表示。

表示項目：

- state name
- status
- fact
- output（JSON）
- startedAt / completedAt
- incoming/outgoing edges
- join inputs（Joinの場合）

---

## 12. ユーザーフロー（Playground）

1. YAML を編集
2. Validate（定義エラーを表示）
3. Save（definition作成）
4. Start（workflow起動）
5. Graph を見る
6. Wait に対して Event を送信
7. 実行状態を追う
8. Cancel で停止
9. ノード詳細でデバッグ

---

## 13. MVP 実装順（最短）

1. WorkflowEditor（Monaco） + Validate/Save
2. RunnerPanel（Start/Cancel/Event）
3. GraphView（ReactFlow + dagre）
4. NodeInspector
5. Polling で snapshot/graph 更新
6. 表示ルール（Wait/Cancel強調、Fork/Joinまとまり）

---

## 14. 将来拡張

- WebSocket/SSE によるライブ更新
- Drag & Drop workflow builder
- Graph replay（履歴再生）
- テンプレートギャラリー
- Workflow 共有リンク
- Execution metrics / timeline

---

## 15. 成功条件

Playground が以下を満たすこと。

- YAML を書けばすぐ動かせる
- Fork/Join が一目で理解できる
- Wait/Resume/Cancel が “操作点” として分かる
- 失敗/キャンセルが即座に判別できる
- 実行結果（output）を追える
