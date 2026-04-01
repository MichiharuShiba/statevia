# 定義仕様

Version: 1.0
Project: 実行型ステートマシン

---

Core-Engine が受け付けるワークフロー定義の YAML/JSON 仕様。**states 形式**と**nodes 形式**の二通りを扱う。

- **States 形式**: 状態名をキーにした `states` マップ。エンジンが直接ロード・コンパイルする（現行実装）。
- **Nodes 形式**: ノードの配列 `nodes`。UI やエディタ向け。実行時には states 形式へ変換して利用する想定。

---

## 1. States 形式

### 1.1 基本構造

```yaml
workflow:
  name: <string>

states:
  <StateName>:
    action: <ActionId>            # 任意。Core-API の Action Registry に登録された ID（例: order.create）
    on:                          # 事実駆動の遷移（Fact → 遷移）
      <Fact>:
        next: <StateName>        # 単一遷移
        fork: [<StateName>, ...]  # 並列開始
        end: true                 # ワークフロー終了
    wait:                         # 待機（オプション）
      event: <EventName>
    join:                         # 合流（オプション）
      allOf: [<StateName>, ...]
```

- **workflow.name**: ワークフロー名（任意、デフォルト "Unnamed"）。
- **states**: 状態名 → 状態定義のマップ。各状態は `on`（遷移）、`wait`（待機）、`join`（合流）のいずれかまたは組み合わせを持つ。
- **action**: 任意。定義登録時に Core-API が Registry へ照合し、未登録の ID はエラーになる。**省略時**は組み込みの `noop`（即時完了）と同等。**`wait` を指定する状態では `action` と併記できない**。

### 1.2 遷移（on）

- **on**: 事実名（例: `Completed`, `Joined`, `Failed`）をキーに、遷移先を指定する。
  - **next**: 次に遷移する状態名（1 つ）。
  - **fork**: 並列に開始する状態名のリスト。
  - **end**: `true` でワークフロー終了。

### 1.3 Wait（待機）

- **wait.event**: 再開に使うイベント名。Resume 時にこのイベント名で `PublishEvent` するとその状態が再開する。

### 1.4 Join（合流）

- **join.allOf**: 完了を待つ状態名のリスト。すべてが完了すると `Joined` 事実が発生し、`on.Joined` で次へ遷移できる。

### 1.5 例（States 形式）

```yaml
workflow:
  name: HelloWorkflow

states:
  Start:
    on:
      Completed:
        fork: [Prepare, AskUser]

  Prepare:
    on:
      Completed:
        next: Join1

  AskUser:
    wait:
      event: UserApproved
    on:
      Completed:
        next: Join1

  Join1:
    join:
      allOf: [Prepare, AskUser]
    on:
      Joined:
        next: Work

  Work:
    on:
      Completed:
        next: End

  End:
    on:
      Completed:
        end: true
```

### 1.6 例（input を使った States 形式）

`input` は、遷移で入る直前の候補 input に適用される。  
`path` の単一ショートハンドと、複数キーのマップ形式をサポートする。

```yaml
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

- `Start` の output が `{ payload: { value: 42 } }` のとき、`ExtractPayload` の input は `42` になる。
- `$.payload.value` が見つからない場合、`ExtractPayload` の input は `null` になる。
- `input` 定義がある状態では、定義で構築した値のみが input になる（未指定フィールドの自動マージはしない）。

### 1.6.1 input マップ形式（複数/ネスト/リテラル）

```yaml
states:
  B:
    input:
      foo: $.a
      foo.bar: $.a.b
      title: "my song"
      retry: 2
      enabled: true
      note: null
```

- `$` または `$.` で始まる文字列はパス式
- それ以外はリテラル
- `foo.bar` はネストオブジェクトとして構築される（後勝ち）
- パス式は `Level1Validator` と同じ単純 JSONPath 制約（`$` または `$.seg1.seg2`、セグメントは英数字と `_`）に従う。
- `${...}` 形式のテンプレート文字列は受理しない（states/nodes ともに同一ルール）。

### 1.6.2 ユーザー定義状態（IState）との関係

- 各状態の output は `IState<TInput, TOutput>.ExecuteAsync(...)` の戻り値。
- 次状態 input の決定ルール:
  - `input` 未定義: 直前 output をそのまま渡す
  - `input` 定義あり: `input` の評価結果のみを渡す
- `input` 定義ありの場合、マッピングしない output の値は引き継がれないため、必要な値は明示的に `input` へ記述する。

### 1.7 例（Fork/Join と input）

Fork の各分岐先・Join 後の次状態でも `input.path` を適用できる。

```yaml
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

- `Start` の output が `{ shared: "fork-value" }` の場合、`A` と `B` の input はどちらも `"fork-value"`。
- `Join1` 後の候補 input は `{ A: <Aのoutput>, B: <Bのoutput> }` の辞書。
- `AfterJoin` の `path: $.A` により、`A` の output だけを抽出して input に渡す。

---

## 2. Nodes 形式

### 2.1 基本構造

ルートに **nodes** 配列があり、各要素が `id` と `type` を持つ。`workflow` メタデータと任意で `controls`（cancel/resume イベント）を併記する。

```yaml
version: 1

workflow:
  id: <string>
  name: <string>
  description: <string>   # 任意

controls:                  # 任意
  cancel:
    event: <EventName>
  resume:
    event: <EventName>

nodes:
  - id: <nodeId>
    type: start | end | action | wait | fork | join
    label: <string>        # 任意
    # 型ごとのプロパティ（下記）
```

### 2.2 ノード型とプロパティ

| type   | 必須プロパティ     | 任意・備考 |
|--------|--------------------|------------|
| start  | next               | 開始ノード。1 つのみ。 |
| end    | —                  | 終端ノード。 |
| action | action, next       | input, onError.next, label 等。 |
| wait   | event, next        | timeout, onTimeout.next。 |
| fork   | branches           | 2 要素以上の配列。 |
| join   | next               | mode: all 等。 |

- **start**: `next` で次ノード ID。
- **end**: `next` なし。
- **action**: `action` はアクション参照（例: `order.create`）。`next` で次ノード。`input` で入力マップ。
- **wait**: `event` で待機イベント名。`next` で再開後の次ノード。`timeout`（ISO 8601 duration）でタイムアウト指定可。
- **fork**: `branches` に並列ブランチのノード ID の配列。
- **join**: すべてのブランチの完了を待ち、`next` へ進む。

### 2.3 例（Nodes 形式・抜粋）

```yaml
version: 1

workflow:
  id: order-workflow
  name: Order Processing Workflow

nodes:
  - id: start
    type: start
    label: Start
    next: createOrder

  - id: createOrder
    type: action
    label: Create Order
    action: order.create
    next: waitPayment

  - id: waitPayment
    type: wait
    label: Wait Payment
    event: payment.completed
    next: endSuccess

  - id: forkFulfillment
    type: fork
    label: Parallel Fulfillment
    branches:
      - prepareShipment
      - notifyUser

  - id: joinFulfillment
    type: join
    label: Join Fulfillment
    mode: all
    next: shipOrder

  - id: endSuccess
    type: end
    label: Completed
```

### 2.3.1 例（Nodes 形式 + input）

`action` ノードの `input` は **States 形式と同じ規則**とする。パスは **`$` / `$.seg1.seg2`** のみ（`Level1Validator` の単純 JSONPath 制約に準拠）。**`${input.x}` のような `${...}` テンプレは採用しない**（`.workspace-docs/specs/done/v2-definition-spec.md` および `v2-nodes-to-states-conversion-spec.md` に合わせる）。

`input.path` ショートハンド、またはキーごとの `$.` 参照で、遷移直前の候補 input を抽出できる。

```yaml
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

- `start` の output が `{ shared: "fork-value" }` なら `a` / `b` は `"fork-value"` を受け取る。
- `join1` 後の候補 input は `{ a: <aのoutput>, b: <bのoutput> }`。
- `afterJoin` は `path: $.a` で join 辞書から `a` の output を抽出する。

- **label**, **description**, **tags**, **ui** は UI/エディタ用。エンジンは無視してよい。
- **metadata** をルートに置く場合もエンジンは無視してよい。

### 2.4 States 形式との対応

Nodes 形式は、実行前に **states 形式の CompiledWorkflowDefinition に変換**して利用する想定。変換レイヤーが nodes の `id`/`type`/`next`/`event`/`branches` を states の `on`/`wait`/`join` にマッピングする。実装上は Core-API の `NodesWorkflowDefinitionLoader` が nodes を `WorkflowDefinition` に変換し、states 形式は `StateWorkflowDefinitionLoader` が読み込む。

---

## 3. ルール（共通）

- 状態名（states 形式）またはノード ID（nodes 形式）は一意とする。
- 自己遷移（A → A）は禁止。
- Join の allOf / branches は既存の状態・ノードを参照する。
- Fork と Join は制御構造であり、実行順序の保証範囲は実装に依存する。
- Wait は指定イベントで再開する待機を表す。

---

## 4. 検証レベル（States 形式）

エンジンが States 形式に対して行う検証の目安。

**LEVEL 1**

- 構文・参照整合性
- 自己遷移の禁止

**LEVEL 2**

- 開始状態からの到達可能性
- 循環 Join の禁止
- 明示的依存関係の強制

Nodes 形式の検証は、変換後の states に対して同様のレベルを適用するか、変換前の nodes 用ルールを別途定義する。
