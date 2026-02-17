# Wait / Cancel Specification

This document describes waiting and cancellation semantics.

## Wait

Wait suspends state execution until a specified event occurs.

- Wait introduces a waiting scope.
- Resume continues the same state execution.
- Each wait can be resumed only once.

## Cancel

- Cancellation is cooperative.
- The engine never forcefully aborts running states.
- Cancelled is emitted only when execution actually stops.
- Dependent states are automatically cancelled.

## States

- Waiting states do not complete until resumed.
- If all active states are waiting, the workflow is considered paused.

---

# 日本語

## Wait / Cancel 仕様

本ドキュメントでは、待機とキャンセルのセマンティクスについて説明します。

## Wait（待機）

Wait は指定されたイベントが発生するまで状態実行を一時停止します。

* Wait は待機スコープを導入します。
* Resume は同じ状態実行を継続します。
* 各 Wait は一度だけ再開できます。

## Cancel（キャンセル）

* キャンセルは協調的です。
* エンジンは実行中の状態を強制終了しません。
* Cancelled は実行が実際に停止したときにのみ発行されます。
* 依存状態は自動的にキャンセルされます。

## 状態

* 待機状態は再開されるまで完了しません。
* すべてのアクティブな状態が待機中の場合、ワークフローは一時停止とみなされます。