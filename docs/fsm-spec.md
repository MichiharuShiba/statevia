# FSM Specification

statevia uses a fact-driven finite state machine.

## Core Concept

Transitions are evaluated by:

(State, Fact) -> TransitionResult

Facts represent actual execution results:

- Completed
- Failed
- Cancelled
- Joined

Requests (e.g., cancel requests) are not facts.

## FSM Characteristics

- Deterministic transitions
- No implicit state transitions
- Self transitions are forbidden
- Transitions are precompiled into O(1) lookup tables

## Transition Result

A transition result may:

- Start new states
- End the current state
- Trigger fork or join logic

---

# 日本語

## FSM 仕様

statevia は事実駆動型有限状態機械を使用します。

## コアコンセプト

遷移は以下によって評価されます：

(状態, 事実) -> 遷移結果

事実は実際の実行結果を表します：

* Completed（完了）
* Failed（失敗）
* Cancelled（キャンセル済み）
* Joined（結合済み）

リクエスト（例：キャンセルリクエスト）は事実ではありません。

## FSM の特性

* 決定論的遷移
* 暗黙の状態遷移なし
* 自己遷移は禁止
* 遷移は O(1) ルックアップテーブルに事前コンパイルされる

## 遷移結果

遷移結果は以下を行うことができます：

* 新しい状態を開始する
* 現在の状態を終了する
* Fork または Join ロジックをトリガーする