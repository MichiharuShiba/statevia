# Statevia

- Version: 1.0.0
- 更新日: 2026-04-02
- 対象: Statevia プロダクトの全体像・コンセプト（入口文書）
- 関連: `.workspace-docs/40_plans/10_in-progress/v2-modification-plan.md`、`.workspace-docs/30_specs/`、`docs/`

---

**Statevia** は、複雑なプロセスを **明示的・観測可能・制御可能** にするワークフロー実行基盤です。

Statevia は宣言的に定義されたワークフローを実行し、その実行をリアルタイムで可視化します。

> 配置ポリシー: 本ファイルは計画・全体方針の入口として `.workspace-docs/40_plans/10_in-progress/` で管理する。仕様の正本は `.workspace-docs/30_specs/`、進行中タスクは `.workspace-docs/50_tasks/10_in-progress/` を参照する。

---

## Philosophy

Statevia は、ひとつの考えに基づいています。

> **状態には理由がある。**

ワークフローの一歩一歩は、何かが起きたから存在します。

Statevia はこれを **Fact（事実）** と **Transition（遷移）** で明示的にモデル化します。

```text

State
↓
Fact
↓
Transition
↓
Next State

```

これにより、ワークフローの振る舞いは **予測可能・観測可能・デバッグ可能** になります。

---

## なぜ Statevia か?

多くのワークフローシステムはオーケストレーションに焦点を当てます。

Statevia は **実行の透明性** に焦点を当てます。

主な考え方:

- 明示的な **Fork / Join**
- イベント駆動の **Wait / Resume**
- 決定的な **FSM 実行**
- 視覚的な **Execution Graph（実行グラフ）**

このシステムは、**ワークフローが動いている様子を見えるようにする** ことを目指して設計されています。

---

## Core Concepts

### Workflow Definition

ワークフローは宣言的に定義します。

```yaml
workflow:
  name: approval
  initialState: Start

states:

  Start:
    on:
      Completed:
        fork: [Review, Audit]

  Review:
    wait:
      event: ReviewDone
    on:
      ReviewDone:
        next: Join

  Audit:
    on:
      Completed:
        next: Join

  Join:
    join:
      allOf: [Review, Audit]
    on:
      Joined:
        next: Approve

  Approve:
    on:
      Completed:
        end: true
```

---

### Fact Driven Execution

状態の実行は **Fact** を生み出します。

Fact が遷移を駆動します。

利用可能な Fact:

- Completed
- Failed
- Cancelled
- Joined

---

### Fork / Join

並列実行は明示的です。

```text

A
│
Fork
├─ B
└─ C
│
Join
│
D

```

---

### Execution Graph（実行グラフ）

すべてのワークフロー実行は **ExecutionGraph** を生成します。

このグラフは以下を記録します:

- 状態の実行
- 遷移
- fork / join の構造

グラフはリアルタイムで可視化できます。

---

## アーキテクチャ

Statevia は 3 層で構成されています。

```text

UI
↓
Core API
↓
Core Engine

```

### Core Engine

ワークフロー実行を担うランタイムです。

Components:

- WorkflowEngine
- TransitionTable (FSM)
- JoinTracker
- Scheduler
- StateExecutor
- ExecutionGraph

---

### Core API

ワークフロー操作のための REST API を提供します。

Examples:

```text

POST /definitions
POST /workflows
POST /workflows/{id}/events
POST /workflows/{id}/cancel
GET /workflows/{id}
GET /workflows/{id}/graph

```

---

### UI

UI はワークフロー実行を可視化します。

Features:

- Workflow Editor（ワークフローエディタ）
- Execution Graph Viewer（実行グラフビューア）
- Workflow Runner（ワークフローランナー）
- Node Inspector（ノードインスペクタ）

---

## Example Execution

```text

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

```

実行はグラフとして見えます。

---

## Design Goals

Statevia は次の原則に沿って設計されています。

- 宣言的ワークフロー
- 決定的実行
- 明示的並行性
- 実行の透明性
- 視覚的デバッグ

---

## ロードマップ

### Phase 1

Core Engine

### Phase 2

Core API

### Phase 3

Workflow Playground

### Phase 4

Developer SDK

### Phase 5

Distributed Runtime（分散ランタイム）

### Phase 6

Statevia SaaS Platform

---

## ドキュメント

`docs` ディレクトリを参照してください。

```text

docs/
├ architecture.md
├ statevia-architecture-v1.md
├ statevia-system-diagram.md
├ core-engine-spec.md
├ engine-runtime-spec.md
├ workflow-definition-spec.md
├ execution-graph-spec.md
├ core-api-spec.md
├ ui-spec.md
└ db-schema.md

```

---

## Vision

Statevia は **普遍的なワークフロー実行基盤** となることを目指しています。

開発者はワークフローを宣言的に定義します。

Statevia がそれを確実に実行し、その実行を見えるようにします。

---

## 変更履歴

| 版 | 日付 | 内容 |
|----|------|------|
| 1.0.0 | 2026-04-02 | メタブロック整備。 |

## License

MIT
