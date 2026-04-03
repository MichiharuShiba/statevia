# nodes 形式 → states 形式 変換仕様（v2 Phase 5）

- Version: 1.0.0
- 更新日: 2026-04-02
- 対象: nodes YAML → `WorkflowDefinition`（states）への機械変換（Phase 5 MVP）
- 関連: `.workspace-docs/30_specs/20_done/v2-definition-spec.md`、`.workspace-docs/50_tasks/20_done/v2-u10-nodes-states-discrimination.md`、`v2-modification-plan.md`（Phase 5）

---

## 概要

- **目的**: `.workspace-docs/30_specs/20_done/v2-definition-spec.md` の **nodes ベース YAML** を、既存の Core-Engine が解釈する **`WorkflowDefinition`（`workflow` + `states`）** に機械変換し、既存の `Level1Validator` / `Level2Validator` / `DefinitionCompiler` パイプラインをそのまま通す。
- **関連決定**: 判別ルールは **U10**（`.workspace-docs/50_tasks/20_done/v2-u10-nodes-states-discrimination.md`）。改修計画の位置づけは **v2-modification-plan.md** Phase 5。実装は本仕様の「MVP スコープ」に従う。

## 1. 非目標（本書で扱わないこと）

- Engine のランタイム変更（新しい Fact、timeout スケジューラ、controls の意味づけなど）。
- UI の編集体験やグラフレイアウト（`ui` は透過的に無視）。
- JSON Schema バリデータの実装（仕様上の整合は変換前チェックで担保）。

---

## 2. 形式の判別（U10 の要約）

YAML / JSON をルートオブジェクトとしてパースしたあと、次を適用する。

| 条件                                                        | 扱い                                                          |
| ----------------------------------------------------------- | ------------------------------------------------------------- |
| ルートに `nodes` があり、値が **配列**                      | **nodes 形式**として変換パイプラインへ                        |
| 上記以外                                                    | **states 形式**として既存 `StateWorkflowDefinitionLoader` へ（現行どおり） |
| `nodes`（配列）と `states`（オブジェクト）が **両方** 存在  | **エラー**（曖昧なため受理しない）                            |
| nodes 形式として扱うが `nodes` が空配列、または必須キー欠落 | **エラー**                                                    |

states 形式側のルートに誤って `nodes` が付いている場合、配列とみなされ nodes 扱いになる **U10 の既知リスク**は、states 作者向けドキュメントで注意喚起する（本仕様では挙動を変えない）。

---

## 3. 用語と対応

- **node id**: nodes 配列要素の `id`。変換後の **states の状態名**（キー）に **そのまま**用いる（大文字小文字・ハイフン等は nodes 定義に従い、Engine の状態名としてそのまま有効であること）。
- **事実名（Fact）**: action / fork 直後の成功遷移は **`Completed`**。**wait** は待機解消後も FSM へ渡る事実は **`Completed`**（`on.Completed` で次へ）。**`wait.event`** は `PublishEvent` 名との照合用で、**`on` のキーとは別**（`docs/core-engine-fsm-spec.md`）。join 合流後は **`Joined`**。

### 3.1 不変ルール（MVP 完了後も変更しない）

次の制約は **nodes 形式の変換仕様として恒久**とする（将来バージョンでも破らない。別 DSL を導入する場合は本仕様の外）。

| ルール            | 内容                                                                                         |
| ----------------- | -------------------------------------------------------------------------------------------- |
| **end と `next`** | **`type: end` のノードに `next` キーが存在してはならない**。存在する場合は常にエラーとする。 |
| **end の個数**    | **`type: end` のノードはワークフロー全体でちょうど 1 つ**。0 個も 2 個以上もエラーとする。   |

**注**: `.workspace-docs/30_specs/20_done/v2-definition-spec.md` の記載例は **複数の end**（例: `endSuccess` / `endCancelled`）や **§7 で現状エラーとするフィールド**を含む。当該例は **スキーマ例・説明用**であり、本変換仕様の不変ルール・MVP スコープとそのまま整合しない。実データやテストでは **end が 1 つ**のグラフを用いる。

---

## 4. workflow メタデータ

| 入力（nodes ルート）                  | `WorkflowDefinition.Workflow.Name` |
| ------------------------------------- | ---------------------------------- |
| `workflow.name` が非空文字列          | その値                             |
| それ以外で `workflow.id` が非空文字列 | `workflow.id`                      |
| どちらも無い                          | `"Unnamed"`                        |

nodes 形式の `workflow` オブジェクトは必須（`v2-definition-spec.md`）。`version` は **整数 `1`** であることを変換前に検証し、それ以外はエラーとする（将来バージョンは別仕様）。

---

## 5. ノード型ごとの変換（MVP）

以下は **MVP で必ず実装する**対応である。記載のないキーは §7 を参照。

### 5.0 `action` の省略（start / end / fork）

Core-API の `ActionExecutorFactory`（`api/Statevia.Core.Api/Application/Definition/ActionExecutorFactory.cs`）は、**`wait` がなく `action` が空（未指定・空白）のとき** `WellKnownActionIds.NoOp` を解決し、即時 `Completed` 相当の executor を返す。`DefinitionCompilerService.ValidateRegisteredActions` も **空の `action` は検証対象外**である。

したがって **start / end / fork から生成する状態では `action` キーを省略してよい**（本仕様の推奨）。読みやすさのため **`action: noop` を明示してもよい**が、**必須ではない**。

**補足**: Engine 単体で `DictionaryStateExecutorFactory`（状態名→executor の辞書）だけを使う経路では、省略ではなく辞書登録が必要である。nodes 変換の主対象は **Core-API 経由のコンパイル**であり、上記フォールバックを前提とする。

### 5.1 `type: start`

- **必須**: `next`（node id）。
- **生成する state**（状態名 = 当該 node の `id`）:
  - **`action` は省略する**（§5.0。即時 `Completed` は NoOp 解決に依存）。
  - `on`:
    - `Completed`: `{ next: <next> }`

### 5.2 `type: end`

- **生成する state**:
  - **`action` は省略する**（§5.0）。
  - `on`:
    - `Completed`: `{ end: true }`
- **`next` キーが存在してはならない**（§3.1 不変ルール。スキーマ上 optional でも **永久に受理しない**）。

### 5.3 `type: action`

- **必須**: `action`（アクション ID）、`next`。
- **生成する state**:
  - `action`: 入力どおり。
  - `input`: §6 のルールに従い states と同一形で写す（省略可。正規化は行わない）。
  - `on`:
    - `Completed`: `{ next: <next> }`

### 5.4 `type: wait`

- **必須**: `event`、`next`。
- **生成する state**:
  - `wait`: `{ event: <event 文字列> }` — **`PublishEvent` の名前と一致**させる（`EventProvider` / `WaitOnlyState` が待機解除に使用）。
  - `on`:
    - **`Completed`: `{ next: <next> }`** — 実装では待機解消後も状態実行の成功終了は **`Fact.Completed`** として FSM に渡る（`WorkflowEngine.ScheduleStateAsync` → `ProcessFact(..., Fact.Completed, ...)` → `Fsm.Evaluate`）。**`on.<event 文字列>` は現行エンジンでは使われない**。

（`.workspace-docs/30_specs/20_done/v2-workflow-definition-spec.md` §6 および `docs/core-engine-fsm-spec.md` の Wait 節と整合すること。）

### 5.5 `type: fork`

- **必須**: `branches`（2 個以上の node id）。
- **生成する state**:
  - **`action` は省略する**（§5.0）。
  - `on`:
    - `Completed`: `{ fork: [ ... branches 順序維持 ... ] }`

### 5.6 `type: join`

- **必須**: `next`。
- **必須（MVP）**: `join.allOf` を §5.7 のアルゴリズムで決定できること。
- **生成する state**:
  - `join`: `{ allOf: [ ... ] }`（§5.7）
  - `on`:
    - `Joined`: `{ next: <next> }`
- `mode` が存在し **`all` 以外**の場合は MVP では **エラー**（スキーマ上は `all` のみ）。

### 5.7 join の `allOf`（MVP アルゴリズム）

**対象**: `type: join` のノード `J`。

1. グラフ上、`branches` が **ちょうど 1 つの fork ノード `F`** のみを探索し、かつ `F.branches` の **各要素 `b`** について、ノード `b` の `next` が **すべて `J` の id** であること。
2. 上を満たす `F` が **一意**に決まるとき、`J` の `join.allOf` は **`F.branches` の配列（順序維持）**とする。
3. 満たさない場合（複数 fork が候補、候補なし、一部の branch の `next` が `J` でない等）は **エラー**とし、メッセージで「MVP では 1 つの fork から直接 `next` で入る join のみ」と明示する。

---

## 6. `input`（states と同一の `$.` のみ）

### 6.1 対象

`type: action` の `input` のみ（`.workspace-docs/30_specs/20_done/v2-definition-spec.md` の actionNode.input。同ファイルの JSON Schema も **パスは `$.` 形式に統一**している）。

### 6.2 ルール

- nodes の `input` は **`.workspace-docs/30_specs/20_done/v2-workflow-definition-spec.md` §5.1「input」と同一の意味**とする。
- パス参照は **`$` または `$.seg1.seg2`**（セグメントは英数字と `_`）**のみ**。`Level1Validator.IsValidSimpleJsonPath` を満たすこと。
- **`${input.x}` のようなテンプレート（`${...}`）形式は採用しない**。変換器は **正規化も代替も行わない**。該当する文字列値（例: `"${input.orderId}"`）が存在する場合は **エラー**とする。
- **現時点で `${...}` を将来採用する予定はない**（別表現が必要になった場合は別仕様で検討）。
- リテラル（数値・真偽・null・オブジェクト・配列）および **`$` / `$.` で始まるパス文字列**は、states と同様に **そのまま** `StateInputDefinition` に写す。

---

## 7. MVP で受理しないフィールド（明示エラー）

### 7.1 現在の挙動（MVP）

次のいずれかが **存在する**場合、**現段階（MVP）**では変換を中止し **422 / 400 相当のわかりやすいメッセージ**を返す（HTTP は Core-API の契約に合わせる）。

| フィールド  | 所在                                                 | MVP 以降の位置づけ                                   |
| ----------- | ---------------------------------------------------- | ---------------------------------------------------- |
| `controls`  | ルート                                               | §7.2（将来仕様で検討・実装予定）                     |
| `timeout`   | wait ノード                                          | 〃                                                   |
| `onTimeout` | wait ノード                                          | 〃                                                   |
| `onError`   | action ノード                                        | 〃                                                   |
| `output`    | action ノード                                        | 〃                                                   |
| `next`      | **end** ノード                                       | **不変ルール（§3.1）— 将来も受理しない**             |
| （値の形）  | **action** の `input` 内の文字列が **`${` で始まる** | **§6 — `${...}` 形式は不採用。正規化しない。エラー** |

### 7.2 将来仕様として検討し実装予定の要素

`controls` / `timeout` / `onTimeout` / `onError` / `output` については、**現状は MVP のためエラーとする**が、**仕様として別途整理したうえで実装予定**とする（Engine・API・イベント意味の設計が前提）。詳細なマッピングや優先度は本書の改訂または別タスク（例: modification-plan の C14 系）で扱う。

§11「将来拡張」と整合させ、実装時は本節を更新する。

### 7.3 無視してよいフィールド（MVP）

- `label`, `description`, `tags`, `ui`, `metadata`（ルートおよびノード上）

---

## 8. 変換前の構造チェック（推奨）

次を **変換前**に検証する（`Level1` / `Level2` に落ちる前に、nodes 固有のメッセージを返すため）。

1. **`type: start` がちょうど 1 つ**。
2. **node id の一意性**（大文字小文字の扱いは states と同じ **OrdinalIgnoreCase** で衝突禁止）。
3. **`next` / `branches` で参照される id は、いずれかのノードの `id` と一致**。
4. **start からの到達性**: start の `next` から辿れるノードが全ノードをカバー（孤立ノード禁止）。
5. **end ノードの個数**: **`type: end` がちょうど 1 つ**（§3.1）。0 個・2 個以上はいずれも **エラー**。

---

## 9. 処理パイプライン（実装配置）

1. ルート判別（§2）。
2. nodes なら §8 の構造チェック → §5〜§7 に従い **`WorkflowDefinition` を構築**（メモリ上。YAML 文字列に再シリアライズする必要はない）。
3. 既存の `Level1Validator` → `Level2Validator` → `ValidateRegisteredActions` → `DefinitionCompiler`。
4. `compiled_json` の形は **states 経路と同一**（既存の `DefinitionCompilerService` のシリアライズ対象と互換）。

**配置ポリシー**（実装方針・B 案）: **nodes 形式の知識は Core-API のみ**。Engine は **`IDefinitionLoader`（states 用 `StateWorkflowDefinitionLoader`）** と **`WorkflowDefinition`** に限定する。API は **`NodesWorkflowDefinitionLoader` : `IDefinitionLoader`** と **`IDefinitionLoadStrategy`**（既定: `DefinitionLoadStrategy`）で形式判別とローダ委譲を行い、**`WorkflowDefinition` 構築後**は既存の `Level1` / `Level2` / `DefinitionCompiler` をそのまま利用する（YAML の states へのシリアライズは不要）。

---

## 10. テスト要件（受け入れ）

| ID  | 内容                                                                                                                       |
| --- | -------------------------------------------------------------------------------------------------------------------------- |
| T1  | §12 の **サンプル A（線形）** 相当がコンパイル可能（**end は 1 のみ**、§7.1 の禁止フィールドなし）                         |
| T2  | §12 の **サンプル C（fork/join）** で §5.7 が満たされ、`Joined` 遷移が生成される                                           |
| T3  | `nodes` と `states` 両方キーありでエラー                                                                                   |
| T4  | action の `input` に `$.input.x` 形式のパスがそのまま通り、§12 サンプル D でコンパイル可能。`${...}` を含む input はエラー |
| T5  | `timeout` / `onError` / `controls` のいずれかでエラー（§7.2 の「現段階はエラー」）                                         |

---

## 11. 将来拡張（本仕様の外）

§7.2 で「仕様検討・実装予定」とした項目の技術的な落とし込み例:

- `onError` → `Failed` Fact への遷移（Engine と Action Registry の契約整理が前提）。
- `timeout` / `onTimeout`（Wait のスケジューラと event_store の意味づけ）。
- `controls` と API の cancel / resume の対応。
- `output` の states / reducer との対応。
- 複数 fork からの合流、`join.mode` の拡張。
- **`$.` 以外の入力マッピング表現**（別 DSL・テンプレート等）— **現時点で採用予定はない**。必要になれば states / nodes 双方の仕様を同時に改訂する。

**複数 end のグラフ**は不変ルール（§3.1）により本変換では許容しない。分岐ごとに見かけ上の終端が必要な場合は、**単一 end に収束するグラフ**で表現するか、将来別仕様で検討する。

---

## 12. マッピングサンプル（nodes MVP → states）

以下は **§3.1（end 1 つ・end に `next` なし）**および **§7.1（禁止フィールドなし）**を満たす例である。`workflow.name` は nodes 側で与え、states 側の `workflow` には **name のみ**（`initialState` は Engine が参照整合から推定するため省略可能な想定。手書き states では `initialState` を明示する例も `v2-workflow-definition-spec.md` にある）。

**注**: start / end / fork 由来の状態は **`action` 省略**（§5.0）。実行時は Core-API の `ActionExecutorFactory` が **NoOp** を解決する。YAML 例では行数を抑えるため省略形とする。

### サンプル A — 線形（start → action → end）

**nodes（MVP）**

```yaml
version: 1
workflow:
  name: LinearSample
  id: linear-sample
nodes:
  - id: start
    type: start
    next: doWork
  - id: doWork
    type: action
    action: order.create
    next: endNode
  - id: endNode
    type: end
```

**変換後 states（同等の `WorkflowDefinition`）**

```yaml
workflow:
  name: LinearSample
states:
  start:
    on:
      Completed:
        next: doWork
  doWork:
    action: order.create
    on:
      Completed:
        next: endNode
  endNode:
    on:
      Completed:
        end: true
```

### サンプル B — wait

**nodes（MVP）**

```yaml
version: 1
workflow:
  name: WaitSample
nodes:
  - id: start
    type: start
    next: waitPay
  - id: waitPay
    type: wait
    event: payment.completed
    next: endNode
  - id: endNode
    type: end
```

**変換後 states**

```yaml
workflow:
  name: WaitSample
states:
  start:
    on:
      Completed:
        next: waitPay
  waitPay:
    wait:
      event: payment.completed
    on:
      Completed:
        next: endNode
  endNode:
    on:
      Completed:
        end: true
```

**注（wait の `event` と `on`）**: `wait.event: payment.completed` は **`POST .../events` 等で送るイベント名**と一致させる。`on` の遷移キーは実装上 **`Completed`**（待機解消後の正常終了に対応する FSM 事実）。`on.payment.completed` と書いても **遷移テーブルは参照されない**（過去の誤記に注意）。

### サンプル C — fork / join（§5.7 想定）

**nodes（MVP）**

```yaml
version: 1
workflow:
  name: ForkJoinSample
nodes:
  - id: start
    type: start
    next: fork1
  - id: fork1
    type: fork
    branches: [branchA, branchB]
  - id: branchA
    type: action
    action: foo.a
    next: join1
  - id: branchB
    type: action
    action: foo.b
    next: join1
  - id: join1
    type: join
    next: endNode
  - id: endNode
    type: end
```

**変換後 states**

```yaml
workflow:
  name: ForkJoinSample
states:
  start:
    on:
      Completed:
        next: fork1
  fork1:
    on:
      Completed:
        fork: [branchA, branchB]
  branchA:
    action: foo.a
    on:
      Completed:
        next: join1
  branchB:
    action: foo.b
    on:
      Completed:
        next: join1
  join1:
    join:
      allOf: [branchA, branchB]
    on:
      Joined:
        next: endNode
  endNode:
    on:
      Completed:
        end: true
```

### サンプル D — action の `input`（`$.` 形式・states と同一）

**nodes（MVP）**

パスは **`$.input.*`** のみ（`${input.*}` は書かない）。

```yaml
version: 1
workflow:
  name: InputSample
nodes:
  - id: start
    type: start
    next: create
  - id: create
    type: action
    action: order.create
    input:
      orderId: $.input.orderId
      userId: $.input.userId
    next: endNode
  - id: endNode
    type: end
```

**変換後 states**（`input` は **同一内容**で写すのみ）

```yaml
workflow:
  name: InputSample
states:
  start:
    on:
      Completed:
        next: create
  create:
    action: order.create
    input:
      orderId: $.input.orderId
      userId: $.input.userId
    on:
      Completed:
        next: endNode
  endNode:
    on:
      Completed:
        end: true
```

---

## 13. 変更履歴

| 日付       | 内容                                                                                                                                         |
| ---------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| 2026-03-29 | 初版。§3.1 不変ルール、§7.2 将来検討、§12 マッピングサンプル。§6 は `${...}` 不採用・`$.` のみ（states と同一）、§7.1 / T4 / サンプル D 整合 |
| 2026-03-29 | §5.0: start/end/fork は `action` 省略可（Core-API `ActionExecutorFactory` の NoOp フォールバック）。サンプルから `action: noop` を削除       |
| 2026-03-29 | wait: `on` は `Completed`（実装準拠）。§3・§5.4・サンプル B 修正。FSM / wait / v2-workflow-definition-spec §6 と整合                         |
| 2026-03-29 | §9: 配置を API 側（`IDefinitionLoadStrategy` / `NodesWorkflowDefinitionLoader`）に更新（Engine は `IDefinitionLoader` の states のみ） |
| 2026-04-02 | メタブロック整備。文書状態はフォルダで表現（先頭の「状態」行を除去）。 |
