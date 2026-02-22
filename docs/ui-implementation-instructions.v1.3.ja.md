# UI 具体的実装指示 v1.3（Codex向け / graphId連携＋列配置＋標準UX）

Version: 1.3
Project: 実行型ステートマシン
Target: services/ui (Next.js + TS + Tailwind)
Scope: List View + Graph View（React Flow + dagre）
Goal: “仮表示”から “定義に基づく表示”へ移行

---

## 0. 不変原則（必ず守る）

- **Cancel wins**（Cancel最強）
- UIは core の結果を描画するだけ（優先順位判断は core 側）
- `cancelRequestedAt != null` の場合、進行系操作を無効化＋理由表示
- RUNNING は控えめ、WAIT/RESUME/CANCEL は強調
- Fork/Join は “まとまり” を視覚で表現

---

## 1. 新要件（v1.3の核）

### 1.1 graphId から静的 ExecutionGraph 定義を引く

- UIは `ExecutionDTO.graphId` を見て、対応する静的定義（nodes/edges/forkJoinGroups）をロードする
- これにより、Graph View の edges/レイアウト/グループが「仮」ではなく定義ベースになる

### 1.2 branch列配置（Fork→Join）を明確にする

- Fork から複数ブランチへ分岐する場合、ブランチは **縦並び（上下）**に配置する
- Join はブランチの右側で合流する
- Hello Sample（公式イメージ）を再現できることが望ましい

### 1.3 React Flow 標準UXを固定

- fitView（初期表示で全体が収まる）
- MiniMap 表示
- Controls（zoom in/out, fitView）
- 背景（Background grid、薄め）

---

## 2. 静的グラフ定義（UI側の新規成果物）

### 2.1 ファイル配置（必須）

- `services/ui/app/graphs/definitions/`
  - `hello.graph.ts`（まず1つ実装：Hello Sample）
  - 将来追加：`<graphId>.graph.ts`

### 2.2 定義の型（必須）

`app/graphs/types.ts` を作り以下を定義：

- GraphDefinition:
  - graphId: string
  - nodes: GraphNodeDef[]
  - edges: GraphEdgeDef[]
  - groups?: GraphGroupDef[]（Fork/Joinまとまり）
  - layoutHints?: LayoutHints（任意）

- GraphNodeDef:
  - nodeId: string
  - nodeType: string（Start/Task/Wait/Fork/Join/Success/Failed/Canceled…）
  - label?: string
  - branch?: string（任意：ブランチ識別子）

- GraphEdgeDef:
  - from: string
  - to: string
  - kind?: "normal" | "fork" | "join"（任意）

- GraphGroupDef:
  - groupId: string
  - label: string（例：Fork-Join Block）
  - nodeIds: string[]

- LayoutHints（任意だが v1.3 では導入推奨）
  - direction: "LR"
  - branchOrder?: string[]（branchの表示順）
  - nodeSizeOverrides?: Record<nodeId, { w:number, h:number }>
  - groupPadding?: { x:number, y:number, header:number }

### 2.3 graphId 解決（必須）

`app/graphs/registry.ts` を作り、

- `getGraphDefinition(graphId): GraphDefinition | null`
  を提供する。
  未定義の場合は v1.2 の仮エッジ生成にフォールバック。

---

## 3. Graph View 入力合成（実行状態とのマージ）

### 3.1 マージ規則（必須）

- core の `ExecutionDTO.nodes` は “状態” を持つ
- 静的定義の nodes は “構造（nodeType/edge/branch）” を持つ
- UIは次で合成する：
  - 表示ノード集合 = 静的定義 nodes
  - 各ノードの status は core 側ノード状態を nodeId で join
  - core に存在しない nodeId は status=IDLE とみなす

### 3.2 nodeType ソース

- 静的定義が優先
- core の nodeType は補助（定義がない場合のみ利用）

---

## 4. レイアウト（dagre） v1.3：branch列配置を固定

### 4.1 基本（v1.2踏襲）

- rankdir=LR
- ranksep=90
- nodesep=50

### 4.2 ブランチ配置（必須）

ブランチを “上下に分ける” ため、以下の規則を適用：

- 静的定義で nodeDef.branch が付いたノードは、その branch ごとに “列” として扱う
- dagre入力の前に “branch anchor” を挿入してレーン化しても良い（実装簡易なら yオフセットで調整でも可）

最低要件（簡易でOK）：

1. dagreで一次配置（xのみ揃えたい）
2. branchごとに yOffset を決めて加算
   - branchOrder に従い上から順に並べる
   - 例：branchIndex \* 220px を加算

Join は branch の右端に来るように（dagre edges で吸着させる）

- fork->branchHead
- branchTail->join
  を edges で保証する

### 4.3 Hello Sample を定義で再現（必須）

`hello.graph.ts` は以下を含む（最小）：

- nodes:
  - Start (start)
  - Task A (task-a)
  - Fork (fork-1)
  - Task B (task-b) branch="b"
  - Task C (task-c) branch="c"
  - Join (join-1)
  - Success (success)
- edges:
  - start -> task-a
  - task-a -> fork-1
  - fork-1 -> task-b (fork)
  - fork-1 -> task-c (fork)
  - task-b -> join-1 (join)
  - task-c -> join-1 (join)
  - join-1 -> success

- groups:
  - groupId="parallel-1"
  - label="Fork/Join"
  - nodeIds = [fork-1, task-b, task-c, join-1]

---

## 5. GroupNode v1.3（定義ベース）

### 5.1 グループソース優先順位

- 静的定義 groups があればそれを使う
- なければ v1.2 の grouping.ts で推定

### 5.2 bbox算出（v1.2踏襲）

- bbox + padding（x=40, y=30, header=28）で算出
- groupの label を表示（左上）

---

## 6. React Flow 標準UX（必須）

Graph View は以下を必ず有効化：

- fitView: true（初回）
- fitViewOptions:
  - padding: 0.2
  - minZoom: 0.2, maxZoom: 1.5（目安）
- MiniMap: 表示（node色は status によって変えるのは任意）
- Controls: 表示（Zoom, Fit）
- Background: grid（薄め）

操作：

- node click → selection
- canvas click → selection解除
- wheel zoom 有効

---

## 7. 表示モード切替（固定）

- ViewToggle は `List | Graph`
- Graph の状態（zoom/position）は View切替で保持して良い（任意）

---

## 8. 実装ファイル指示（v1.3）

### 8.1 新規追加（必須）

- app/graphs/types.ts
- app/graphs/registry.ts
- app/graphs/definitions/hello.graph.ts
- app/lib/mergeGraph.ts（定義×実行状態のマージ）
- app/lib/layout.ts（v1.3：branch yOffset + dagre）
- app/lib/grouping.ts（定義groups優先＋fallback）

### 8.2 変更（必須）

- app/components/NodeGraphView.tsx
  - registryからdefinition取得→merge→layout→render
  - React Flow UX（MiniMap/Controls/Background/fitView）
- app/page.tsx（graphId表示/定義未発見時の注意表示など）

---

## 9. 受け入れ条件（v1.3）

### 9.1 定義連携

- execution.graphId が "hello" の場合、Graph View は `hello.graph.ts` 定義に基づいて描画される
- core の実行状態（WAITING/RUNNING/CANCELED等）が、定義ノードに反映される
  - 例：task-c が WAITING なら Graph の task-c ノードが WAIT強調＋Resume CTA

### 9.2 ブランチ列配置

- Fork の後、Task B / Task C が上下に分かれて配置される
- Join がその右側で合流している見た目になる

### 9.3 UX

- 初回表示で fitView される
- MiniMap / Controls / Background が表示される
- nodeクリックで Detail が同期する

### 9.4 Cancel wins

- Cancel後は Resume が disabled + 理由表示
- CANCELED 表示が最強（視覚強弱）

### 9.5 品質

- `npm run build` が通る
- TS strict エラーなし
- 409/422/500 は Toast 表示

---

## 10. Codex 依頼文（v1.3 / コピペ用）

---

あなたはこのリポジトリのUI実装者です。
`docs/ui-implementation-instructions.v1.3.ja.md` に従い、services/ui を改修してください。

必須:

- graphId から静的グラフ定義をロード（registry）
- 定義×実行状態をマージして Graph View を描画
- Hello Sample（graphId="hello"）の定義を実装し、Fork→2ブランチ→Join→Success を再現
- dagre + branch yOffset で上下レーン配置を実装
- GroupNode は定義 groups を優先し、bbox+paddingで描画
- React Flow UX：fitView / MiniMap / Controls / Background
- WAIT node 内に Resume CTA を埋め込む
- Cancel wins の強弱・無効化理由表示・Toast（409/422/500）
- `npm run build` が通る

変更範囲:

- services/ui のみ

作業後:

- 変更点要約
- 動作確認手順（helloの作り方も）
- 主要ファイル一覧

---
