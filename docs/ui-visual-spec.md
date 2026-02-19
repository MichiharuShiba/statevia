# UI 可視化仕様（UI Visual Specification）

本ドキュメントは、Statevia における ExecutionGraph の公式 UI 表現仕様を定義する。
本仕様は、Statevia の管理 UI / ビジュアライザ実装における **規範仕様（Normative Spec）** である。

---

## 1. スコープ

本ドキュメントで定義する内容:

- Node（ノード）の視覚ルール
- Edge（エッジ）の視覚ルール
- Fork / Join のグルーピング表現
- Wait / Resume / Cancel の強調ルール
- Hello Workflow に基づく公式 UI リファレンス
- 視覚的優先順位ルール

本ドキュメントでは定義しない内容:

- API スキーマ
- FSM 内部仕様
- エンジン内部実装

---

## 2. Node（ノード）の視覚仕様

### 2.1 Node 種別

| statusType | 説明 |
|------------|------|
| Task       | 実行可能な通常ステート |
| Wait       | 待機ステート |
| Fork       | 並列分岐 |
| Join       | 並列合流 |

---

### 2.2 Node Status の視覚優先度

| Status     | 視覚優先度 | 表示方針 |
|------------|------------|----------|
| Waiting    | 最優先     | 強く強調表示 |
| Cancelled  | 高         | 強い警告表現 |
| Failed     | 高         | 明確なエラー表現 |
| Running    | 中         | 控えめな動的表現 |
| Completed  | 低         | 背景に溶け込む表現 |
| Idle       | 最低       | ニュートラル表示 |

設計思想:

- Waiting / Cancelled / Failed はユーザーの注意・操作対象となるため強調する  
- Running は一時的状態のため主張しすぎない  
- Completed は履歴として参照可能であればよい  

---

## 3. Edge（エッジ）の視覚仕様

### 3.1 Edge 種別

| Edge Type | 意味 |
|----------|------|
| Next     | 通常の遷移 |
| Resume   | イベントによる再開 |
| Cancel   | キャンセル伝播 |

---

### 3.2 Edge の描画ルール

| Edge Type | 描画ルール |
|----------|------------|
| Next     | 実線 |
| Resume   | 破線 + イベント名表示 |
| Cancel   | 太線 + Cancel 表示 |

Resume Edge に表示する情報:

- eventName
- 必要に応じて payload の要約

Cancel Edge に表示する情報:

- cancel.reason
- cancel.cause（任意）

---

## 4. Fork / Join の視覚グルーピング

Fork / Join は UI 上で **必ず視覚的にグルーピング** する。

### ルール

- Fork と Join は枠で囲ってひとまとまりとして表現する
- Fork と Join の対応関係が一目で分かる配置とする
- UI 実装によっては折りたたみ可能としてもよい
- 並列ブランチは視覚的に分離しつつ、同一グループであることが分かるようにする

概念図:

```txt

┌──────────── Fork ────────────┐
│                              │
│   [TaskB]      [TaskC]        │
│      │            │           │
│    [Wait]         │           │
│      ▲            │           │
│   Resume(Event)   │           │
└─────────────┬────────────────┘
▼
┌──────── Join ────────┐
│        [TaskD]       │
└─────────────────────┘

```

---

## 5. Wait / Resume / Cancel の強調方針

### 5.1 Wait

- Wait ノードは「停止している」ことが視覚的に分かる表現にする
- 以下を表示できること:
  - 待機中のイベント名
  - 待機理由（存在する場合）
- UI 上でユーザーの注意を引く表現とする

---

### 5.2 Resume

- Resume は Next 遷移と明確に区別される必要がある
- Resume Edge にはイベント名を必ず表示する
- ユーザー操作による復帰であることが視覚的に伝わること

---

### 5.3 Cancel

- Cancel は UI 上で最も強い意味を持つ操作として表現する
- Resume と Cancel が競合する場合は Cancel が優先される
- Cancel には以下の情報を紐づけて参照可能とする:
  - cancel.reason
  - cancel.cause

---

## 6. 公式 UI リファレンス（Hello Workflow）

本節の ExecutionGraph 表現は Statevia における **公式 UI リファレンス** である。

### 論理構造

```txt

[Start] ──▶ [TaskA]
│
▼
┌──────── Fork ────────┐
│                      │
[TaskB]                [TaskC]
│                      │
[Wait]                    │
│  ▲                     │
│  └── Resume(Event)     │
│                      │
└──────────┬───────────┘
▼
┌──── Join ────┐
│   [TaskD]    │
└─────────────┘

```

### ステータス例（Wait 中）

```txt

[TaskA] Completed
[TaskB] Waiting
[Wait]  Waiting
[TaskC] Completed
[TaskD] Idle

```

---

## 7. UI 実装に対する拘束ルール

- 本ドキュメントは Statevia UI の公式仕様である
- UI 実装は以下を破ってはならない:
  - Fork / Join のグルーピング
  - Wait / Resume / Cancel の視覚的区別
  - Status の視覚優先度
- UI 実装は以下を自由に決めてよい:
  - 配色
  - レイアウトエンジン
  - アニメーション

ただし、本仕様で定義された **意味論的な視覚ルールは維持されること**。

---

## 8. 将来的な拡張余地

本仕様は将来的に以下の拡張を想定する:

- タイムライン表示
- 実行履歴のリプレイ UI
- 実行グラフの差分表示
- 複数 ExecutionGraph の比較 UI
