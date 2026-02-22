# UI 具体的実装指示 v1.1（Codex向け / デザイン詳細版）

Version: 1.1
Project: 実行型ステートマシン
Target: services/ui (Next.js + TS + Tailwind)
Scope: List View + Graph View（React Flow）

---

## 0. 不変原則（必ず守る）

- **Cancel wins**：CancelはResumeより強い（Cancel最強）
-
- UIは core の状態結果を描画するだけ。優先順位ロジックは core 側
- ただし UX として `cancelRequestedAt != null` の場合、進行系UIを無効化し「理由」を表示する
- RUNNING は控えめ（目立たせない）

---

## 1. 画面構成（固定）

### 1.1 レイアウト

- Header（固定）
- - Execution ID input + Load
  - 右端に **Cancel（最上位CTA）**（常時表示）
  - ViewToggle（List / Graph）
- Main（2カラム）
  - 左：List View / Graph View（切替）
  - 右：Node Detail（選択ノード詳細 + アクション）

### 1.2 余白・サイズ（推奨固定値）

- 外枠カード：rounded-2xl + shadow-sm + border-zinc-200
- カラム間ギャップ：gap-4
- 主要セクション padding：p-4
- ボタン高さ：py-2
- Nodeカード最小幅（Graphノード）：w-[220px]（目安）

---

## 2. 色・強弱（トークン仕様 / Tailwindで表現）

### 2.1 ステータストークン（必須）

以下を `app/lib/statusStyle.ts` として固定し、List/Graph共通で使う。

- CANCELED（最強）
  - badge: bg-red-600 text-white
  - border: border-red-600
  - bg: bg-red-50
  - icon: ✕（または Cancelアイコン）
  - emphasisRank: 100

- FAILED（強いがCancelより弱い）
  - badge: bg-red-500 text-white
  - border: border-red-400
  - bg: bg-red-50
  - icon: ⚠
  - rank: 80

- WAITING（ユーザーの関心対象）
  - badge: bg-amber-500 text-white
  - border: border-amber-400
  - bg: bg-amber-50
  - icon: ⏸
  - rank: 70

- RUNNING（控えめ）
  - badge: bg-zinc-200 text-zinc-800
  - border: border-zinc-200
  - bg: bg-white
  - icon: ▶
  - rank: 30
  - 表現：opacity-80 / text-zinc-600（軽く）

- SUCCEEDED/COMPLETED（穏やか）
  - badge: bg-emerald-600 text-white
  - border: border-emerald-300
  - bg: bg-emerald-50
  - icon: ✓
  - rank: 40

- READY（中立）
  - badge: bg-blue-600 text-white
  - border: border-blue-300
  - bg: bg-blue-50
  - icon: •
  - rank: 20

- IDLE（中立）
  - badge: bg-zinc-300 text-zinc-800
  - border: border-zinc-200
  - bg: bg-white
  - icon: ○
  - rank: 10

### 2.2 ボタンの優先度（必須）

- Cancel（最上位CTA）
  - bg-red-600 hover:bg-red-700 text-white font-semibold
  - Header右端に固定
- Resume（次点CTA：WAITINGノードのみ）
  - bg-amber-500 hover:bg-amber-600 text-white font-semibold
  - Node Detail と Graph Node 内に配置
- Refresh/Load 等の補助
  - border border-zinc-200 hover:bg-zinc-50

---

## 3. List View（実装固定）

### 3.1 ソート規則（必須）

Nodes の表示順は以下を優先：

1. WAITING
2. CANCELED
3. FAILED
4. RUNNING（控えめ）
5. READY
6. SUCCEEDED
7. IDLE

※ `emphasisRank` を使って降順ソートしてよい。

### 3.2 行/カードの表現（必須）

- WAITING：背景 amber-50、強調
- CANCELED/FAILED：背景 red-50、強調
- RUNNING：背景白、opacity-80（控えめ）

---

## 4. Graph View（React Flow）実装指示（デザイン詳細）

### 4.1 依存追加（必須）

- reactflow（必須）
- dagre（任意：自動レイアウト）
- 可能なら classnames などは不要（Tailwind直書きでOK）

### 4.2 ノードコンポーネント仕様（必須）

`NodeGraphView.tsx` 内で Custom Node を2種類実装する：

1. **ExecutionNode（通常ノード）**
2. **GroupNode（Fork/Joinまとまり背景）**

#### 4.2.1 ExecutionNode（UI詳細）

- 外枠：
  - rounded-2xl border-2 shadow-sm
  - border色は statusStyle.border
  - 背景は statusStyle.bg
- ヘッダ行：
  - 左：icon（statusStyle.icon）
  - 中：nodeType（小さめ text-xs font-semibold）
  - 右：Badge（status）
- 本文：
  - nodeId（font-mono text-xs）
  - attempt / waitKey（存在する場合のみ表示）
- フッタ（CTA領域）：
  - WAITING のときだけ **Resume ボタンを埋め込む**
  - ボタンは「幅いっぱい」推奨（w-full）

#### 4.2.2 RUNNINGの控えめ表現（必須）

- ExecutionNode に `data.status === RUNNING` の場合：
  - 全体 `opacity-80`
  - ラベルは `text-zinc-600`
  - アニメは付けない（付けるなら極弱の pulse 程度）

#### 4.2.3 選択状態（必須）

- 選択されたノードは外枠に `outline outline-2 outline-zinc-400` を付与
- 選択はクリックで行い、右の Node Detail と同期する

### 4.3 エッジ（今回の最低要件）

- edges が無い場合：
  - 表示はノードのみでも可
  - ただしノードが “整列” するように配置（grid / dagre / 簡易横並び）
- edges が導入できる場合：
  - stroke は薄め（text-zinc-300 相当）
  - アニメーションは禁止（RUNNINGを強く見せないため）

### 4.4 Fork/Join まとまり（GroupNode仕様）

#### 4.4.1 GroupNode（UI詳細）

- GroupNode は “背景領域” として描画
- 表現：
  - rounded-2xl
  - bg-zinc-100/60
  - border border-zinc-200 border-dashed
  - 左上に小ラベル（例：`Parallel Block` / `Fork-Join`）
- z-index：
  - GroupNode を背面、ExecutionNode を前面（React Flow の parent/extent を利用）

#### 4.4.2 groupingロジック（必須）

`app/lib/grouping.ts` に以下の関数を作る：

- `buildGroups(nodes: ExecutionDTO["nodes"]): { groups: Group[], nodeToGroup: Record<nodeId, groupId> }`

v1.1 では仮ルールを固定：

- nodeId に `fork-` を含むノードを起点に
- nodeId に `join-` を含むノードを終点に
- その間にあるノードを group に入れる（間の定義は仮で良い：prefix一致や並び順など）
- 仮ルールが難しい場合は、**存在する fork/join ノードだけを1つのグループにまとめる**でもOK（受け入れ条件は満たす）

重要：

- grouping は差し替え可能な関数として独立させる（後で core の graph 定義と結合するため）

### 4.5 Graph上のCTA

- Cancelは Header の固定CTA（Graph内に置かない）
- Resumeは WAITING node 内に埋め込む（Graphの価値）
- cancelRequestedAt がある場合：
  - Resumeボタン disabled
  - disabled理由（tooltip or 小文字注釈）を node 内に表示

---

## 5. Node Detail（UI詳細）

- nodeId（font-mono）
- nodeType、statusバッジ
- waitKey（あれば）
- canceledByExecution（trueなら「Execution Cancel により収束」文言を表示）
- Resumeボタン
  - WAITING かつ cancelRequestedAt==null かつ 非terminal のときだけ有効
  - 無効時は理由を表示（固定文言）
    - Cancel要求済み
    - ノードがWAITINGではない
    - Executionが終端

---

## 6. 状態メッセージ（固定文言）

- cancelRequestedAt != null の場合：
  - 「Cancel要求済みのため、Resumeなど進行系操作はできません」
- terminal の場合：
  - 「Executionは終了しています」

---

## 7. 実装ファイル指示（v1.1）

### 7.1 追加（必須）

- app/lib/statusStyle.ts（トークン実装）
- app/lib/grouping.ts（仮グループ）
- app/components/ExecutionHeader.tsx
- app/components/ViewToggle.tsx
- app/components/NodeListView.tsx
- app/components/NodeGraphView.tsx（React Flow）
- app/components/NodeDetail.tsx
- app/components/Toast.tsx

### 7.2 変更（必須）

- app/page.tsx（2カラム化、View切替、Node選択同期、Disable理由表示）

---

## 8. 受け入れ条件（v1.1）

### 8.1 機能

- List/Graph切替が動作する
- Graph上でノードをクリックすると Node Detail が同期する
- WAITING ノードの中に Resume CTA がある
- Cancel要求後は Resume が disabled になり、理由が表示される

### 8.2 視覚強弱

- CANCELED が最強（赤太枠、バッジ、背景）
- FAILED は赤だが Cancel より控えめ（太さ/色/ラベルで差）
- WAITING はアンバーで強調され、CTAが見える
- RUNNING は控えめ（opacity-80、薄い表現）

### 8.3 Fork/Join

- Graph View に “まとまり背景（GroupNode）” が表示される（仮でも可）
- groupロジックが grouping.ts に分離されている

### 8.4 品質

- `npm run build` が通る
- TS strict でエラー無し
- エラーはToastで表示される（409/422/500）

---

## 9. Codex 依頼文（v1.1 / コピペ用）

---

あなたはこのリポジトリのUI実装者です。
`docs/ui-implementation-instructions.v1.1.ja.md` に従い、services/ui を改修してください。

必須:

- List View + Graph View（React Flow）を実装
- 状態トークン（Cancel最強 / Wait強調 / Running控えめ / FailはCancelより弱い）を共通化
- WAITING node 内に Resume CTA を埋め込む（Graphでも見える）
- Fork/Join のまとまり背景（GroupNode）を表示し、groupingロジックは `app/lib/grouping.ts` に分離
- cancelRequestedAt 以降は進行系操作を disabled + 理由表示
- Toastで 409/422/500 を表示
- `npm run build` が通る

変更範囲:

- services/ui のみ

作業後:

- 変更点要約
- 動作確認手順
- 主要ファイル一覧

---
