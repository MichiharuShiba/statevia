# Statevia Playground 仕様

Statevia Playground は、Workflow を **作成・実行・可視化**するための開発環境である。

Playground の目的は以下である。

- Workflow DSL の編集
- Workflow の実行
- ExecutionGraph の可視化
- Workflow のデバッグ

Playground は Statevia を体験するための **主要な開発ツール**となる。

---

## 1. Playground アーキテクチャ

Playground は以下の構成で動作する。

UI  
↓  
Core API  
↓  
Core Engine

```text

UI (Playground)
│
│ REST / WebSocket
│
▼
Core API
│
│ Engine Interface
│
▼
Core Engine

```

---

## 2. Playground 画面構成

Playground は以下の画面で構成される。

Workflow Editor  
Workflow Runner  
Execution Graph Viewer  
Node Inspector

---

## 3. Workflow Editor

Workflow DSL を編集する画面。

主な機能

- YAML エディタ
- シンタックスハイライト
- 定義のバリデーション
- 自動フォーマット

例

```yaml
workflow:
  name: sample
  initialState: Start

states:
  Start:
    on:
      Completed:
        next: A

  A:
    on:
      Completed:
        end: true
```

---

## 4. Workflow Runner

Workflow を実行する画面。

ユーザー操作

- Workflow 開始
- Workflow 停止
- Event 送信

利用する API

```text
POST /workflows
POST /workflows/{id}/events
POST /workflows/{id}/cancel
```

---

## 5. Execution Graph Viewer

Workflow 実行を **グラフとして可視化する UI**。

グラフ構造

Nodes
State 実行を表す

Edges
State 間の関係

Edge types

Next
Fork
Join

---

## 6. グラフ表示ルール

ExecutionGraph の UI 表現。

Fork / Join は **構造として分かりやすく表示する**。

例

```text

      A
      │
     Fork
    /    \
   B      C
    \    /
     Join
      │
      D
```

表示ルール

Fork
横方向に branch を展開

Join
branch を合流させる

Wait / Resume
ユーザー操作として強調表示

Failed / Cancelled
目立つ色で表示

Running
控えめなアニメーション表示

---

## 7. Node Inspector

グラフのノードをクリックすると
State 実行の詳細が表示される。

表示情報

State 名

実行ステータス

Fact

Output

開始時間

終了時間

---

## 8. リアルタイム実行表示

Playground は Workflow 実行を **リアルタイムに表示する**。

Graph 更新イベント

NodeStarted
NodeCompleted
NodeFailed
NodeCancelled
JoinTriggered

UI はこれらのイベントを受け取り
ExecutionGraph を更新する。

---

## 9. 実行操作

ユーザーは以下の操作が可能。

Start
Cancel
Event 送信

例

Approve
Reject
Retry

---

## 10. デバッグ機能

Playground は Workflow デバッグを支援する。

機能

実行履歴表示

Fact トレース

State Output 表示

エラー可視化

---

## 11. Playground レイアウト

画面レイアウト例

```text

+---------------------+-----------------------+
|                     |                       |
|   Workflow Editor   |   Execution Graph     |
|                     |                       |
+---------------------+-----------------------+
|                                             |
|        Workflow Runner / Controls           |
|                                             |
+---------------------------------------------+

```

---

## 12. 開発者の利用フロー

典型的な利用フロー

1. Workflow を記述する
2. 定義を検証する
3. Workflow を開始する
4. ExecutionGraph を観察する
5. Event を送信する
6. Workflow をデバッグする

---

## 13. 開発体験の目標

Statevia Playground は以下を実現する。

即時フィードバック

Workflow 構造の視覚的理解

簡単なデバッグ

低い学習コスト

---

## 14. 将来拡張

Playground の将来機能

ビジュアル Workflow エディタ

Workflow テンプレート

実行リプレイ

パフォーマンスメトリクス

Workflow 共有機能

---

## 15. ビジョン

Statevia Playground は

Workflow ベースシステムを構築するための
最高の開発環境になることを目指す。

開発者は

Workflow を設計し
Workflow を実行し
Workflow を理解する

すべてを **1つの環境で行える**。
