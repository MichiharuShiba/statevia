# Statevia Roadmap

- Version: 1.0.0
- 更新日: 2026-04-02
- 対象: Statevia のフェーズロードマップ（Engine → Playground → SaaS）
- 関連: `.workspace-docs/40_plans/10_in-progress/v2-modification-plan.md`

---

Statevia は Workflow Runtime Platform である。

このロードマップは
Statevia を

- OSS Engine
- Workflow Playground
- SaaS Platform

へ発展させるための段階を示す。

---

## Phase 1 — Core Engine

目的

Workflow Engine を完成させる。

Components

Core-Engine

Features

- FSM based execution
- Fork / Join
- Wait / Event
- Scheduler
- ExecutionGraph
- Definition DSL

---

## Phase 2 — Core API

目的

Engine を外部から利用可能にする。

Components

Core-API

Features

REST API

POST /definitions
POST /workflows
POST /workflows/{id}/events
POST /workflows/{id}/cancel
GET /workflows/{id}
GET /workflows/{id}/graph

Persistence

- workflow definitions
- workflow runs
- execution graph snapshot

---

## Phase 3 — Playground

目的

Workflow を可視化する。

Components

UI

Features

Workflow Editor

- YAML editing
- validation

Workflow Runner

- start
- cancel
- send events

Execution Graph Viewer

- fork/join visualization
- wait/resume highlight
- failure/cancel emphasis

Goal

Statevia Playground

---

## Phase 4 — Developer Platform

目的

Workflow をアプリケーションに組み込む。

Features

SDK

- .NET
- TypeScript

CLI

statevia run
statevia deploy
statevia validate

---

## Phase 5 — Distributed Runtime

目的

大規模 Workflow 実行。

Features

- distributed scheduler
- event queue
- worker nodes
- retry policies
- timeout
- compensation

---

## Phase 6 — SaaS Platform

目的

Statevia を SaaS として提供する。

Features

Multi-tenant

Workflow monitoring

Execution history

Graph debugging

Workflow marketplace

---

## Vision

Statevia は次のような存在を目指します。

普遍的なワークフロー実行基盤。

開発者はワークフローを宣言的に記述し、
Statevia がそれを確実に実行する。

---

## 変更履歴

| 版 | 日付 | 内容 |
|----|------|------|
| 1.0.0 | 2026-04-02 | メタブロック整備。 |
