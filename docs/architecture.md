# Architecture

statevia is a definition-driven workflow engine based on a fact-driven FSM.

This document describes the high-level architecture and responsibilities of each layer.

## Overview

Definition (YAML / JSON)
  -> AST
  -> Compiler
  -> FSM / Fork / Join / JoinTracker
  -> Scheduler (parallelism control)
  -> State Executor (async execution)
  -> Execution Graph (observation)

## Layers

### Definition Layer

Responsible for loading and validating workflow definitions.

### Compiler Layer

Transforms definitions into internal runtime structures:

- FSM transition table
- Fork table
- Join trackers

### FSM Layer

Evaluates transitions based on facts:
(State, Fact) -> TransitionResult

### Scheduler Layer

Controls execution order and parallelism.
The engine does not enforce policies beyond concurrency limits.

### Executor Layer

Executes user-defined states asynchronously.

### Execution Graph

Records execution history for debugging and visualization.
This layer is observational and does not affect execution.

---

# 日本語

## 概要

statevia は事実駆動型 FSM に基づく定義駆動型ワークフローエンジンです。

本ドキュメントでは、各レイヤーの高レベルアーキテクチャと責務について説明します。

## レイヤー

### 定義レイヤー

ワークフロー定義の読み込みと検証を担当します。

### コンパイラレイヤー

定義を内部ランタイム構造に変換します：

* FSM 遷移テーブル
* Fork テーブル
* Join トラッカー

### FSM レイヤー

事実に基づいて遷移を評価します：
(状態, 事実) -> 遷移結果

### スケジューラレイヤー

実行順序と並列度を制御します。エンジンは同時実行制限を超えるポリシーは強制しません。

### エグゼキューターレイヤー

ユーザー定義の状態を非同期で実行します。

### 実行グラフ

デバッグと可視化のための実行履歴を記録します。このレイヤーは観測用であり、実行には影響しません。