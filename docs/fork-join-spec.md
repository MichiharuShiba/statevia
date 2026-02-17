# Fork / Join Specification

Fork and Join are control structures for parallel execution.

## Fork

Fork starts multiple states concurrently.

Example:

A -> Fork -> [B, C]

## Join

Join waits for facts from multiple states before proceeding.

Example:

Join(E) = allOf(B, C)

## Rules

- Fork and Join are not states.
- Join is evaluated whenever a dependent state produces a fact.
- Join failure occurs if any required state fails or is cancelled.
- Fork does not guarantee execution order.

## Nested Fork / Join

Fork and Join can be nested and composed.
Join nodes may depend on other join nodes.

---

# 日本語

## Fork / Join 仕様

Fork と Join は並列実行のための制御構造です。

## Fork

Fork は複数の状態を同時に開始します。

例：A -> Fork -> [B, C]

## Join

Join は複数の状態からの事実を待ってから次に進みます。

例：Join(E) = allOf(B, C)

## ルール

* Fork と Join は状態ではありません。
* Join は依存状態が事実を生成するたびに評価されます。
* 必須状態のいずれかが失敗またはキャンセルされた場合、Join は失敗します。
* Fork は実行順序を保証しません。

## ネストされた Fork / Join

Fork と Join はネストして組み合わせることができます。Join ノードは他の Join ノードに依存することができます。