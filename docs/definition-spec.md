# Definition Specification

This document defines the YAML / JSON schema used to describe workflows.

## Basic Structure

```yml
workflow:
  name: <string>

states:
  <StateName>:
    on:
      <Fact>:
        next: <StateName>
        fork: [<StateName>, ...]
    wait:
      event: <EventName>
    join:
      allOf: [<StateName>, ...]
```

## Rules

- State names must be unique.
- Self transitions (A -> A) are not allowed.
- Join targets must reference existing states.
- Fork and Join are control structures.
- Wait introduces a waiting state that resumes on event.

## Validation Levels

LEVEL 1:

- Syntax validation
- Reference integrity
- No self transitions

LEVEL 2:

- Reachability validation
- No circular joins
- Explicit dependency enforcement

---

# 日本語

## 定義仕様

本ドキュメントでは、ワークフローを記述するために使用する YAML / JSON スキーマを定義します。

## 基本構造

（上記 YAML スキーマを参照）

## ルール

* 状態名は一意である必要があります。
* 自己遷移（A -> A）は許可されません。
* Join ターゲットは既存の状態を参照する必要があります。
* Fork と Join は制御構造です。
* Wait はイベントで再開する待機状態を導入します。

## 検証レベル

LEVEL 1：

* 構文検証
* 参照整合性
* 自己遷移の禁止

LEVEL 2：

* 到達可能性検証
* 循環 Join の禁止
* 明示的依存関係の強制