# Execution Graph Specification

The execution graph records workflow execution history.

## Purpose

- Debugging
- Visualization
- Auditing
- Reproducibility

## Graph Model

- Node: State execution
- Edge: Execution relationship (Next, Fork, Join, Resume, Cancel)
- Event: State execution fact or warning

## Characteristics

- The execution graph is append-only.
- It does not affect execution.
- It can be exported as JSON for external visualization.

## Use Cases

- Timeline replay
- Deadlock detection
- Performance analysis

---

# 日本語

## 実行グラフ仕様

実行グラフはワークフロー実行履歴を記録します。

## 目的

* デバッグ
* 可視化
* 監査
* 再現性

## グラフモデル

* ノード：状態実行
* エッジ：実行関係（Next, Fork, Join, Resume, Cancel）
* イベント：状態実行の事実または警告

## 特性

* 実行グラフは追記のみです。
* 実行には影響しません。
* 外部可視化のために JSON としてエクスポートできます。

## ユースケース

* タイムライン再生
* デッドロック検出
* パフォーマンス分析