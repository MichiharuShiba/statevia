# UI 具体的実装指示 v1.2（Codex向け / レイアウト固定＋形状言語）

Version: 1.2
Project: 実行型ステートマシン
Target: services/ui (Next.js + TS + Tailwind)
Scope: List View + Graph View（React Flow + dagre）

---

## 0. 不変原則（必ず守る）

- **Cancel wins**：CancelはResumeより強い（Cancel最強）
- UIは core の状態結果を描画するだけ。優先順位ロジックは core 側
- `cancelRequestedAt != null` の場合、進行系UIを無効化し理由を明示する
- RUNNING は控えめ（目立たせない）
- Fork/Join は **まとまり**（並列ブロック）として見せる

---

## 1. 画面構成（固定）

- Header：Execution ID入力＋Load、右端に Cancel CTA、ViewToggle（List/Graph）
- Main：2カラム
  - Left：List/Graph
  - Right：Node Detail（選択同期）

---

## 2. ステータストークン（v1.1踏襲）

- `app/lib/statusStyle.ts` を共通利用（Cancel最強、Wait強調、Running控えめ）

---

## 3. nodeType 別「形状言語」（必須）

Graph View のノードは `nodeType` で **外形・アイコン・ヘッダラベル**を変える。
実装は `app/lib/nodeAppearance.ts` に分離し、`getNodeAppearance(nodeType)` を提供する。

### 3.1 形状（CSSクラスで表現）

- Start:
  - shape: pill（rounded-full）
  - label: START
- Success / Completed:
  - shape: pill（rounded-full）
  - label: SUCCESS
- Failed:
  - shape: standard（rounded-2xl）＋ warning icon
- Canceled:
  - shape: standard（rounded-2xl）＋ x icon（Cancel最強）
- Task:
  - shape: standard（rounded-2xl）
  - label: TASK
- Wait:
  - shape: extra-rounded（rounded-[22px]）※他より柔らかい
  - label: WAIT
  - CTA を内包（Resume）
- Fork:
  - shape: “tab”風（rounded-2xl + 上部だけ強調ヘッダ）
  - label: FORK
- Join:
  - shape: diamond “風”（疑似でOK）
    - 実装簡易案：`rotate-45` の正方形を背景に置き、内容は `-rotate-45`
  - label: JOIN

### 3.2 アイコン（絵文字でOK）

- Start: ▶
- Task: ▢
- Wait: ⏸
- Fork: ⑂（または ⇄）
- Join: ⑃（または ⇅）
- Success: ✓
- Failed: ⚠
- Canceled: ✕
- Running 表示はアイコンではなく status badge で控えめに

---

## 4. Graph View レイアウト（dagre 固定）

### 4.1 依存追加（必須）

- reactflow
- dagre

### 4.2 レイアウト方針（固定）

- グラフ方向：Left → Right（rankdir = LR）
- ノード間隔：ranksep = 80, nodesep = 40（目安）
- ノードサイズ（固定値を基本に）
  - standard node: width=240, height=120
  - wait node: height=150（CTA領域分）
  - small pill nodes (Start/Success): width=180, height=70
  - join diamond: width=200, height=120（見た目は菱形）

### 4.3 edges が無い場合の仮エッジ生成（必須）

core read-modelに edges が無い場合でも dagre を動かすため、以下の仮エッジ規則で edges を生成する。
このロジックは `app/lib/layout.ts` に分離すること。

仮エッジ規則（v1.2固定）：

1) nodes を `nodeType` の優先順で並べ替える（Start → Task/Wait → Fork/Join → terminal）
2) ソート後に隣接ノードを線で繋ぐ（i → i+1）
3) Fork が存在する場合：
   - Fork の次の2ノードを “branch” として扱い、Fork→branch1, Fork→branch2 を追加
   - Join が存在する場合：
     - branch1→Join, branch2→Join を追加
   - もし branch数が足りない/Joinが無い場合はフォールバックして隣接接続

※ 目的は「見た目の理解」なので完全正確さより **整列とまとまり表現**を優先する。

### 4.4 dagre 実装要件

- `layoutGraph(nodes, edges) => { nodesWithPos, edges }`
- 位置は React Flow 用に `position: { x, y }` を付与
- 再計算は
  - Executionロード時
  - View切替でGraphに入ったとき
  - window resize 時（任意、簡易でOK）

### 4.5 エッジの見た目（固定）

- stroke: 薄い（zinc-300 相当）
- animated: false（RUNNINGを強く見せない）
- markerEnd: 矢印（小さめ）

---

## 5. Fork/Join グルーピング（GroupNode v1.2：サイズ算出まで固定）

### 5.1 groupingロジック（v1.1踏襲＋強化）

- `app/lib/grouping.ts` に `buildGroups(nodes, edges?)` を実装
- edges が仮生成でも良いので、
  - Fork から出る branch を同一グループにする
  - branch の終点が Join なら “Fork-Join Block” としてラベル付け

### 5.2 GroupNode の配置とサイズ（必須）

GroupNode は子ノード群の **bounding box** から算出する。

- groupPaddingX = 40
- groupPaddingY = 30
- groupHeaderHeight = 28（ラベル領域）

算出手順（固定）：

1) グループに属する子ノードの (x, y, w, h) を集計
2) minX/minY/maxX/maxY を求める
3) groupX = minX - groupPaddingX
4) groupY = minY - groupPaddingY - groupHeaderHeight
5) groupW = (maxX - minX) + groupPaddingX*2
6) groupH = (maxY - minY) + groupPaddingY*2 + groupHeaderHeight

表示：

- bg-zinc-100/60
- border border-zinc-200 border-dashed
- rounded-2xl
- 左上にラベル（Fork-Join Block / Parallel Block）

### 5.3 z-index / parent-child（必須）

- React Flow の parentNode / extent を使えるなら使う
- 難しい場合：
  - GroupNode を先に nodes 配列に入れて “背面” とし、ExecutionNode を後に入れて前面にする
  - GroupNode は `selectable: false`, `draggable: false`

---

## 6. ノード内CTA（Graph上の操作導線固定）

- WAIT node 内に Resume CTA を埋め込む
  - ボタンは w-full
  - 無効化時は node 下部に理由文言（text-xs text-zinc-600）
- Cancel は Header の固定CTA（Graph内に置かない）

---

## 7. 無効化理由の固定文言（v1.2）

- cancelRequestedAt != null：
  - 「Cancel要求済みのため、Resumeなど進行系操作はできません」
- node.status != WAITING：
  - 「WAITING 状態のノードのみ Resume できます」
- terminal execution：
  - 「Executionは終了しています」

---

## 8. 実装ファイル指示（v1.2）

### 8.1 追加（必須）

- app/lib/nodeAppearance.ts（nodeType→形状/ラベル/アイコン）
- app/lib/layout.ts（dagreレイアウト＋仮エッジ生成）
- app/lib/grouping.ts（v1.2：Fork/Joinグループ＋bbox算出）
- app/components/NodeGraphView.tsx（React Flow + custom nodes + group node）
- app/components/*（v1.1の分割を維持）

### 8.2 変更（必須）

- app/page.tsx（Graph表示のために layout+group を組み合わせる）
- package.json（reactflow, dagre）

---

## 9. 受け入れ条件（v1.2）

### 9.1 Graphレイアウト

- Graph View でノードが “整列” して表示される（dagre）
- edges が無くても仮エッジで矢印が描画される

### 9.2 形状言語

- nodeType に応じて形状・ラベル・アイコンが変わる（Waitは丸み強、Joinは菱形風など）

### 9.3 グループ

- Fork/Join が存在する場合、背景グループ（GroupNode）が表示される
- GroupNode のサイズは子ノード bbox + padding で算出される

### 9.4 Cancel wins UX

- Cancel後に Resume が disabled + 理由表示
- CANCELED が最強の視覚強弱（赤・太枠・背景）

### 9.5 品質

- `npm run build` が通る
- TS strict でエラー無し
- 409/422/500 は Toast 表示

---

## 10. Codex 依頼文（v1.2 / コピペ用）

---
あなたはこのリポジトリのUI実装者です。
`docs/ui-implementation-instructions.v1.2.ja.md` に従い、services/ui を改修してください。

必須:

- Graph View を React Flow + dagre で実装し、edges が無くても仮エッジ生成で矢印表示
- nodeType別の形状言語（Start/Wait/Fork/Join…）を実装（nodeAppearance.ts）
- Fork/Join のまとまり背景（GroupNode）を bbox + padding で算出して表示（grouping.ts）
- WAIT node 内に Resume CTA を埋め込む
- Cancel wins（Cancel最強、Wait強調、Running控えめ、FailはCancelより弱い）
- cancelRequestedAt 以降の進行系操作を disabled + 理由表示
- Toastで 409/422/500 表示
- `npm run build` が通る

変更範囲:

- services/ui のみ

作業後:

- 変更点要約
- 動作確認手順
- 主要ファイル一覧
  
---