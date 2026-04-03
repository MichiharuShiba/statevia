# Workflow Definition Specification

Workflow Definition は Statevia の Workflow を記述する DSL である。

YAML 形式で定義され、Core-Engine により実行される。

---

## 1. Structure

Workflow 定義は次の構造を持つ。

workflow
states

```text
Example:

workflow:
  name: sample
  initialState: Start

states:
  Start:
    on:
      Completed: { next: A }

  A:
    on:
      Completed: { end: true }

```

---

## 2. Workflow Section

```text
workflow:
  name: string
  initialState: string
```

| field        | description   |
| ------------ | ------------- |
| name         | workflow name |
| initialState | first state   |

---

## 3. States Section

```text
states:
  StateName:
```

State 名は **一意**である必要がある。

---

## 4. State Types

State は以下のタイプを持つ。

| type   | description  |
| ------ | ------------ |
| action | 通常処理     |
| wait   | イベント待ち |
| join   | 分岐合流     |

type を省略した場合 `action` とみなす。

---

## 5. Action State

通常処理。

**Core-API（v1）**: 状態キー直下に **Registry 用の action ID** を `action: <id>` で指定できる（例: `order.create`）。未登録 ID は定義登録時にエラー。省略時は `noop` 相当。**`wait` と `action` は同一状態に両方指定できない。**

```text
states:

  A:
    type: action
    action: order.create
    on:
      Completed: { next: B }
```

State 実行結果は Fact として評価される。

---

## 5.1 input（任意）

**遷移でこの状態に入る直前**に、エンジンが渡す **候補 input**（直列では前状態の output、Fork では Broadcast 値、Join では分岐状態名→output の辞書）に対して、**変換をかけてから** `ExecuteAsync` に渡す場合に指定する。

- **省略時**: 候補 input を **そのまま**渡す（`.workspace-docs/30_specs/20_done/v2-workflow-input-output-spec.md` §3.2）。
- **記述場所**: 受け側の状態キー直下（例: `states.B.input`）。**type: action** を主対象とする（他タイプは実装と整合させる）。

```text
states:

  B:
    type: action
    input:
      # 単一ショートハンド
      path: $.payload.value
      # または複数マッピング
      # foo: $.a
      # foo.bar: $.a.b
      # title: "my song"
    on:
      Completed: { next: C }
```

`input` は次の 2 形式をサポートする。

- **単一ショートハンド**: `path: $.a.b`
- **マップ形式**: `foo: $.a` / `foo.bar: $.a.b` / `title: "my song"` のような複数指定

マップ形式では、文字列値が `$` または `$.` で始まる場合はパス式、それ以外はリテラルとして扱う。

### 5.2 input のサンプル（最小実装）

```text
workflow:
  name: InputMappingSample

states:
  Start:
    on:
      Completed:
        next: ExtractPayload

  ExtractPayload:
    input:
      path: $.payload.value
    on:
      Completed:
        next: End

  End:
    on:
      Completed:
        end: true
```

- `Start` の output が `{ payload: { value: 42 } }` なら `ExtractPayload` の input は `42`。
- パス未解決時は `null`。

### 5.3 Fork/Join での input サンプル

```text
workflow:
  name: ForkJoinInputMappingSample

states:
  Start:
    on:
      Completed:
        fork: [A, B]

  A:
    input:
      path: $.shared
    on:
      Completed:
        next: Join1

  B:
    input:
      path: $.shared
    on:
      Completed:
        next: Join1

  Join1:
    join:
      allOf: [A, B]
    on:
      Joined:
        next: AfterJoin

  AfterJoin:
    input:
      path: $.A
    on:
      Completed:
        end: true
```

- `Start` の output が `{ shared: "fork-value" }` のとき、`A`/`B` は `"fork-value"` を受け取る。
- Join 後の候補 input は `{ A: <Aのoutput>, B: <Bのoutput> }`。
- `AfterJoin` は `path: $.A` で Join 辞書から `A` の output を受け取る。

### 5.4 複数・ネスト・リテラルの input サンプル

```text
workflow:
  name: InputRichSample

states:
  A:
    on:
      Completed: { next: B }

  B:
    input:
      foo: $.a
      foo.bar: $.a.b
      title: "my song"
      count: 2
      enabled: true
      note: null
    on:
      Completed: { end: true }
```

- ドットキー（`foo.bar`）はネストオブジェクトとして構築される。
- パス未解決は `null`。
- キー衝突は後勝ち（記述順）。

---

## 6. Wait State

外部イベント待ち。

```text
states:

  WaitApproval:
    type: wait
    wait:
      event: Approve
    on:
      Completed: { next: NextStep }
```

| field      | description  |
| ---------- | ------------ |
| wait.event | 待機解除に使うイベント名（`PublishEvent` と一致）。FSM の `on` キーとは別。 |
| on.Completed | 待機解消後、状態が正常終了したあと FSM が評価する遷移（実装は事実 `Completed` でルックアップ） |

イベントは Core-API 経由で送信される。

```text
POST /workflows/{id}/events
```

---

## 7. Join State

Forkされた branch を合流する。

```text
states:

  JoinStep:
    type: join
    join:
      allOf: [B, C]
    on:
      Joined: { next: D }
```

| field      | description    |
| ---------- | -------------- |
| join.allOf | join対象 state |

Join はすべての state 完了後に `Joined` fact を発生する。

---

## 8. Transitions

遷移は `on` セクションで定義する。

```text
on:
  Fact: Transition
```

Example:

```text
on:
  Completed: { next: B }
```

---

## 9. Transition Types

Transition は以下を持つ。

| field | description  |
| ----- | ------------ |
| next  | 次の state   |
| fork  | 並列 state   |
| end   | workflow終了 |

---

## 10. Next Transition

通常遷移。

```text
on:
  Completed: { next: B }
```

---

## 11. Fork Transition

並列分岐。

```text
on:
  Completed:
    fork: [B, C]
```

Graph:

```text
A
|
Fork
/ \
B C
```

---

## 12. End Transition

Workflow終了。

```text
on:
  Completed:
    end: true
```

---

## 13. Facts

Statevia Engine が生成する Fact。

| Fact      | meaning         |
| --------- | --------------- |
| Completed | state成功       |
| Failed    | state失敗       |
| Cancelled | stateキャンセル |
| Joined    | join完了        |

---

## 14. Example Workflow

```text
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
      ReviewDone: { next: Join }

  Audit:
    on:
      Completed: { next: Join }

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

## 15. Validation Rules

Workflow 定義は Validator により検証される。

Level1:

- state name uniqueness
- next state existence
- fork / next 同時指定禁止
- end + next 禁止

Level2:

- unreachable states
- join cycles

---

## 16. Execution Model

Workflow execution:

```text
State
↓
Fact
↓
Transition
↓
Next / Fork
↓
Join
```

---

## 17. Design Principles

Workflow DSL は以下を重視する。

1. 可読性
2. 宣言的記述
3. Graph構造の明確化
4. UI可視化との整合

---

## 18. Future Extensions

将来追加可能な機能。

| feature     | description            |
| ----------- | ---------------------- |
| retry       | retry policy           |
| timeout     | timeout control        |
| guard       | conditional transition |
| map         | parallel iteration     |
| subworkflow | nested workflow        |

---

## 19. nodes 形式の参考（input）

本ドキュメントの主対象は `states` 形式だが、UI 向け `nodes` 形式でも `action` ノードの `input.path` を同様に扱う。

```text
version: 1
workflow:
  id: fork-join-inputmap
  name: ForkJoin InputMapping (Nodes)

nodes:
  - id: start
    type: action
    action: seed
    next: fork1

  - id: fork1
    type: fork
    branches: [a, b]

  - id: a
    type: action
    action: branch.a
    input:
      path: $.shared
    next: join1

  - id: b
    type: action
    action: branch.b
    input:
      path: $.shared
    next: join1

  - id: join1
    type: join
    mode: all
    next: afterJoin

  - id: afterJoin
    type: action
    action: finalize
    input:
      path: $.a
    next: end1

  - id: end1
    type: end
```
