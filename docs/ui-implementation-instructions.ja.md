# UI 具体的実装指示（Codex向け）

Version: 1.0
Project: 実行型ステートマシン
Target: services/ui (Next.js + TS + Tailwind)

---

## 0. 前提（必ず守る）

- 本プロジェクトは **Cancel wins**（CancelはResumeより強い）
- UIは「状態の結果」を描画するだけ。優先順位判断は core 側の結果に従う
- ただし UX として、cancelRequestedAt がある場合は **進行系UIを無効化**し、理由を表示する
- 実行中ノード（RUNNING）は控えめに表現する（目立たせない）

---

## 1. 目標（今回のスコープ）

### 1.1 まず動く UI（MVP）

- ExecutionをID指定で読み込める
- ノード一覧が表示され、ノードを選択できる
- Wait状態ノードに対して Resume できる（Cancel要求が無ければ）
- Executionを Cancel できる（最上位の操作）
- 409/422などのAPIエラーをユーザーにわかりやすく表示

### 1.2 次の拡張（今回の実装に含める）

- 2表示モードを提供する
  1) List View（表/カード）
  2) Graph View（React Flow）
- Graph Viewで以下を実現
  - Fork/Joinを「まとまり（グループ）」として表現（親グループノード or 背景領域）
  - Wait/Resume/Cancel は視線誘導される（ボタンが埋め込まれる）
  - Failed / Canceled は一目で判別できる

---

## 2. データソース（API）

UIは Next Route Handler のプロキシを介して core-api を呼び出す（CORS回避）。

- GET  /api/core/executions/:executionId
- POST /api/core/executions/:executionId/cancel
- POST /api/core/executions/:executionId/nodes/:nodeId/resume

レスポンス型は `ExecutionDTO` に従う（services/ui/app/lib/types.ts）

---

## 3. UI画面設計（必須）

### 3.1 画面全体レイアウト

- Header：Execution ID入力＋Load、右側に常時 Cancel ボタン
- Main：左右2カラム
  - 左：Nodes（List / Graph 切替）
  - 右：Node Detail（選択ノードの詳細＋Resumeボタン）

### 3.2 状態表示の強弱（必須）

- CANCELED：最強（赤・太枠・最上位バッジ）
- FAILED：強い（赤系。ただしCancelより控えめ）
- WAITING：目立つ（アンバー/強調、Resume CTA）
- RUNNING：控えめ（淡色、細枠、アニメ控えめ）
- SUCCEEDED/COMPLETED：穏やか（緑、控えめ）
- READY/IDLE：通常（中立）

### 3.3 操作可能性（必須ルール）

- cancelRequestedAt != null の場合：
  - Resumeボタンは disabled
  - 「Cancel要求済みのため操作不可」を明示
- Executionが terminal（COMPLETED/FAILED/CANCELED）の場合：
  - Cancelは disabled
  - Resumeは disabled
- ノードが WAITING 以外のとき：
  - Resumeは disabled（理由表示）

---

## 4. Graph View（React Flow）実装指示（必須）

### 4.1 依存追加

- reactflow を追加（package.json）
- 可能なら dagre で自動レイアウト（簡易でOK）

### 4.2 グラフの入力データ

現時点の core read-model に edges が無い場合は、Graph View は “仮配置”で良い。
ただし以下を満たす：

- ノードは「種類」と「状態」を表示する
- WAITING ノードは node 内に Resume ボタンを表示
- Execution Cancel は画面上部の固定CTA（Graph内ではなくOK）

※ edges/graphId から定義を引ける場合は、それを利用して配置して良い。

### 4.3 Fork/Join のまとまり

edges が無くても「同一prefix」などの仮規則でまとまりを表現して良い。
例（仮規則）：

- nodeId が `fork-*` / `join-*` を含む場合、その間のノードを同一group背景に入れる

本番規則は後で core の graph 定義と結合するため、実装は以下にしておく：

- groupingロジックは `app/lib/grouping.ts` に分離
- 仮規則は関数で差し替えられる

---

## 5. 実装ファイル指示（追加・変更）

### 5.1 追加するファイル

- app/lib/statusStyle.ts
  - status → { badgeClass, borderClass, emphasisRank } を返す
- app/lib/grouping.ts
  - nodes → groups を返す（仮規則でもOK）
- app/components/ExecutionHeader.tsx
- app/components/NodeListView.tsx
- app/components/NodeGraphView.tsx
- app/components/NodeDetail.tsx
- app/components/Toast.tsx
- app/components/ViewToggle.tsx

### 5.2 変更するファイル

- app/page.tsx
  - レイアウトを2カラム化
  - View切替（List/Graph）
  - Node選択状態を共通化
  - Cancel/Resume の disabled 条件・理由表示を統一

---

## 6. エラーハンドリング（必須）

- APIが { error: { code, message, details } } を返したら、
  UIは code と message をユーザーに表示する
- 409 は「状態競合（Cancel wins を含む）」として分かるメッセージにする
- 422 は入力不正として表示する
- 500 は一般エラーとして表示する

---

## 7. 受け入れ条件（Acceptance Criteria）

### 7.1 機能

- Execution ID を入力→Loadで状態表示できる
- WAITING ノードを選択→Resumeが成功する（Cancel要求が無い場合）
- Cancel ボタンで Cancel が成功する
- Cancel後：
  - Resume が disabled になり理由が表示される
  - CANCELED の表示が最優先で目立つ

### 7.2 見た目（強弱）

- CANCELED が FAILED より目立つ
- WAITING が RUNNING より目立つ
- RUNNING は控えめ

### 7.3 Graph View

- List/Graph の切替ができる
- Graph View で各ノードが表示され、状態のバッジが付く
- WAITING ノード内に Resume CTA がある
- Fork/Join が “まとまり” として視覚表現される（仮でも可）

### 7.4 品質

- `npm run build` が通る
- 追加したUIコンポーネントはTypeScript strictでエラーなし
- 主要な状態（ACTIVE/CANCELED/FAILED/WAITING/RUNNING）で表示崩れがない

---

## 8. Codex への依頼文テンプレ（そのまま使える）

以下を Codex に渡して作業させる：

---
あなたはこのリポジトリのUI実装者です。
`docs/ui-implementation-instructions.ja.md` に従い、services/ui を改修してください。

必須:

- List View と Graph View（React Flow）を実装
- Cancel wins の視覚強弱を実装（Cancel最強、Wait強調、Running控えめ）
- cancelRequestedAt 以降は Resume を無効化し理由表示
- 409/422/500 のエラー表示（Toast）
- `npm run build` が通る

変更して良い範囲:

- services/ui 配下のみ

変更してはいけない:

- services/core-api の仕様・挙動を変えること（UI側で吸収する）

作業後:

- 変更点要約
- 動作確認手順（画面操作）
- 主要ファイル一覧

---
