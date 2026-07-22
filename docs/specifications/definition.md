# 定義仕様

| 項目 | 値 |
| --- | --- |
| 種別 | Specification |
| Version | 1.4.1 |
| 更新日 | 2026-07-21 |
| 関連 | [concepts/definition.md](../concepts/definition.md) |

---

## Normative 要約

- **MUST**: publish 時に参照する `action` ID は Catalog に存在すること。未登録はエラー。
- **MUST**: `wait` または `join` を持つ状態に `action` を併記してはならない。
- **MUST**: module alias は大文字小文字を区別せずテナント内で一意であること。
- **SHOULD**: 定義は states 形式で記述し、nodes 形式は UI 編集後に正規化する。
- **禁止**: 実行中に定義版を上書きすること（新版は append のみ）。詳細は [data-integration.md](data-integration.md)。

背景・動機は [Concept: 定義](../concepts/definition.md) を参照。

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
  modules:                      # 任意。module alias → ModuleId
    <alias>: <ModuleId>

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
      all: [<StateName>, ...]
```

- **workflow.name**: ワークフロー名（任意、デフォルト "Unnamed"）。
- **workflow.modules**: 任意。module alias（キー）→ ModuleId（値）のマップ。`action: mail.send` のように alias 付き action 参照を解決する。alias は **大文字小文字を区別せず一意**（重複・空キー・空 ModuleId は Loader 構文エラー）。現状 ModuleId に version 指定はできない（同一 moduleId の複数版共存・版レンジ解決は**未実装**）。
- **states**: 状態名 → 状態定義のマップ。各状態は `on`（遷移）、`wait`（待機）、`join`（合流）のいずれかまたは組み合わせを持つ。
- **action**: 任意。定義登録時に Core-API が Catalog へ照合し、未登録の ID はエラーになる。**省略時**は implicit noop（canonical: `statevia.action.builtin.noop`、即時完了）と同等。Builtin 短名・module alias・FQCN のいずれでも記述できる（§1.1.1）。**`wait` または `join` を指定する状態では `action` と併記できない**。

### 1.1.1 Action ID（canonical 形式と解決）

YAML 上の `action` 参照は syntax parse のあと、Core-API Compiler（`ActionNameResolver`）で **canonical actionId** に正規化され、Action Registry へ照合される。Loader は生の参照文字列を保持し、semantic resolution は Compiler のみが行う。

| 記法（YAML） | 正規化後（canonical） | 例 |
| --- | --- | --- |
| 省略 | `statevia.action.builtin.noop` | action 未指定 |
| Builtin 短名 | `statevia.action.builtin.{name}` | `noop` → `statevia.action.builtin.noop` |
| FQCN（Builtin） | そのまま | `statevia.action.builtin.rest` |
| `{alias}.{actionName}` + `workflow.modules` | `{ModuleId}.{actionName}` | `mail.send` + `mail: com.company.mail` → `com.company.mail.send` |
| 多段 FQCN（alias 未登録） | そのまま | `com.vendor.pkg.actionName` |

Builtin 短名（MVP）:

| 短名 | canonical ID | 概要 |
| --- | --- | --- |
| `noop` | `statevia.action.builtin.noop` | 即時完了（入力をそのまま出力） |
| `sleep` | `statevia.action.builtin.sleep` | `input.duration` で待機 |
| `rest` | `statevia.action.builtin.rest` | HTTPS REST 呼び出し |
| `notify` | `statevia.action.builtin.notify` | email 通知（MVP） |
| `signal` | `statevia.action.builtin.signal` | 実行スコープ内シグナル発行 |
| `publish` | `statevia.action.builtin.publish` | システムトピック発行（MVP stub） |
| `workflow` | `statevia.action.builtin.workflow` | 子ワークフロー起動（`mode: sync` は experimental） |

廃止:

- **`delay5s`** は削除済み。`sleep` + `input.duration`（例: `5s`）へ移行する。

解決に失敗した参照（未知の Builtin 短名・未登録 module alias・Catalog 未登録 ID）はコンパイルエラー（HTTP 422）となる。

**TrustLevel と実行モード（Core-API Policy）:** Catalog の `ActionDescriptor.TrustLevel` と実行環境（`ASPNETCORE_ENVIRONMENT` / `Statevia:ExecutionPolicy:DeploymentProfile`）から `ConfigurableExecutionPolicy` が最終 `ActionExecutionMode` を決定する。Builtin は `Trusted`（InProcess）。filesystem Module は既定 `Community`（本番は OutOfProcess 想定 — Phase 3 Action Host まで実行は `UnsupportedExecutionMode`）。Policy は TrustLevel 下限を緩和できない。

### 1.1.2 Action パラメータと `input` キー

Action の実行パラメータ（例: rest の `url`）および状態への入力マッピングは、いずれも YAML キー **`input:`** で記述する。**`config:` キーは採用しない**（将来導入予定なし）。

- **状態 input マッピング**: 直前状態の output から値を抽出する（§1.6）。`input.path` 単一形式またはキー付きマップ。
- **Action パラメータ**: Builtin / Module action の schema に従う `input` マップ。Core-API Compiler が publication の `inputSchema` で検証する（422 `details`: `state`, `actionId`, `jsonPath` — 機微値は含めない）。**Module 由来 action**（`Visibility != Builtin`）は `ActionPublication` 未登録で **422**。Builtin カスタム登録のみ structured warning（`RequireSchemaForUnpublishedActions` で strict 化可）。
  - **ルートフラット**（フェーズ E）: ルート直下の scalar / 浅い object リテラル（例: rest `headers`）。
  - **ネスト object**（フェーズ F2）: `inputSchema` の `type: object` ノードを再帰検証。`Values` は実行時と同じ論理ツリーへ正規化してから schema を適用する。
  - **ドットキーとネスト map の同等性**: `ship.address: "x"` と `ship: { address: "x" }` は同一論理ツリーとして compile 成功する。正規化衝突（例: `ship` にスカラーと `ship.address` を併記）は 422。`jsonPath` は階層を反映する（例: `$.input.ship.contact.email`）。
  - **Playground UI**: schema 駆動フォームはネスト object をフィールドグループ表示し、保存時は **ネスト map** 形式を既定とする。
- **Schema API**: `GET /v1/actions/schema/{actionId}` で input/output schema と UI metadata を取得。Playground はこれを schema 駆動フォーム生成に利用する。

### 1.1.3 action レベル retry（parse only）

action 状態（`action` を指定する状態 / nodes の `type: action`）では、`input` と **同一階層**に `retry` を宣言できる。Loader は syntax parse のみ行い、**MVP では実行時リトライは適用しない**（将来の Action-level retry 向け）。

```yaml
states:
  CallApi:
    action: rest
    retry:
      limit: 3
      backoff: exponential
      errors: [timeout, 5xx]
    input:
      url: https://example.com/hook
      method: POST
    on:
      Completed:
        next: Done
```

| キー | 型 | 説明 |
| --- | --- | --- |
| `limit` | int（任意） | 最大試行回数 |
| `backoff` | string（任意） | バックオフ戦略（例: `exponential`） |
| `errors` | string[]（任意） | リトライ対象の失敗種別（例: `timeout`, `5xx`） |

制約:

- `retry` を `input` マップ内に書くことは **不可**（Loader 構文エラー）。
- `wait` / `join` 状態では `retry` と `action` を併記できない（Level 1 検証エラー）。

### 1.1.4 Builtin action の input / output（MVP）

各 Builtin の `input` は §1.1.2 の action パラメータとして状態 YAML の `input:` に記述する。`output` は状態完了時に Engine が次状態へ渡す候補 input の元になる（§1.6）。**IO-14**: rest レスポンス body、notify 本文、子 workflow の input 等はログ・一覧 GET に載せない方針とする。

#### noop（`statevia.action.builtin.noop`）

- **input**: 任意（状態 input マッピングの結果をそのまま受け取る）
- **output**: 入力を **そのまま**返す（pass-through）

#### sleep（`statevia.action.builtin.sleep`）

- **input**: `{ duration }` — 必須。`5s` / `500ms` / 数値（ミリ秒）を受理
- **output**: 意味のない完了（`Unit`）。**直前 payload は次状態へ引き継がれない**（pass-through しない）

#### rest（`statevia.action.builtin.rest`）

- **input**:

  | フィールド | 必須 | 説明 |
  | --- | --- | --- |
  | `url` | はい | **HTTPS** の絶対 URL。SSRF 防止のため loopback / プライベート IP / `localhost` 等は拒否 |
  | `method` | はい | HTTP メソッド（例: `GET`, `POST`） |
  | `headers` | いいえ | 文字列値の map → リクエストヘッダ |
  | `body` | いいえ | 文字列または JSON オブジェクト/配列。上限 **1 MiB** |
  | `timeout` | いいえ | 秒（整数）。省略時 **30** |
  | `idempotencyKey` | いいえ | 指定時 `Idempotency-Key` ヘッダに送出 |

- **output**: `{ statusCode, headers, body }` — `body` は文字列（上限 1 MiB で切り詰め）

#### notify（`statevia.action.builtin.notify`）

- **input**:

  | フィールド | 必須 | 説明 |
  | --- | --- | --- |
  | `channel` | はい | MVP は `email` のみ |
  | `to` | はい | 宛先 |
  | `subject` | はい | 件名 |
  | `body` | はい | 本文 |
  | `from` | いいえ | 差出人。省略時はプラットフォーム既定 |

- **output**: `{ channel, messageId? }`
- **接続設定（SMTP 等）**: workflow `input` には含めない。Platform 設定（環境変数 `STATEVIA_SMTP_*` / `Notification:SmtpSettingsSource` 等）で解決する。

#### signal（`statevia.action.builtin.signal`）

- **input**: `{ signal, target? }` — `signal` 必須。`target` は MVP で `current` のみ（省略時 `current`）
- **output**: 意味のない完了（`Unit`）。同一実行内の wait（`wait.event`）再開用に `IEventProvider.Signal` を発行する

#### publish（`statevia.action.builtin.publish`）

- **input**: `{ topic, payload? }` — `topic` 必須。`payload` は任意（MVP では dispatch ログの要約のみ）
- **output**: `{ topic, dispatched: true }` — 外部 bus には未接続（stub）

#### workflow（`statevia.action.builtin.workflow`）

- **input**:

  | フィールド | 必須 | 説明 |
  | --- | --- | --- |
  | `definitionId` | はい | 子定義 ID（display ID または UUID） |
  | `mode` | はい | `async` または `sync` |
  | `input` | いいえ | 子ワークフロー開始 input（JSON 互換） |
  | `timeout` | いいえ | **`mode: sync` のみ**。秒（整数）。省略時 **300**（5 分） |

- **output**: `{ workflowId, displayId, status }`（async / sync 共通）
- **制約**: 現在テナント内の定義・実行に限定。`mode: sync` は **experimental**（ポーリング 200ms）。本番推奨パスでは async を使用する

#### Execution Semantics（signal / publish / wait）

| 概念 | スコープ | Builtin / ノード |
| --- | --- | --- |
| signal | execution-scoped | `signal` action → wait の `event` 再開 |
| publish | system-scoped | `publish` action（MVP stub） |
| wait | execution-scoped | wait ノード `event`（従来どおり） |

Phase 2（未実装）: wait ノード直下に `duration` / `signal` / `event` を排他指定する統合構文。

### 1.2 遷移（on）

- **on**: 事実名（例: `Completed`, `Joined`, `Failed`）をキーに、遷移先を指定する。
  - **next**: 次に遷移する状態名（1 つ）。
  - **fork**: 並列に開始する状態名のリスト。
  - **end**: `true` でワークフロー終了。

### 1.3 Wait（待機）

- **wait.event**: 再開に使うイベント名。Resume 時にこのイベント名で `PublishEvent` するとその状態が再開する。

### 1.4 Join（合流）

- **join.all**: 完了を待つ状態名のリスト。すべてが完了すると `Joined` 事実が発生し、`on.Joined` で次へ遷移できる。
- Join 状態は合成ノードとして実行され、**`action` は指定しない**（`wait` と同様に `action` 併記は Level 1 検証エラー）。

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
      all: [Prepare, AskUser]
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

`input` のパス式（`$` / `$.seg…`）の評価根は **Execution Context 全体**である
（実装型: `WorkflowExecutionContext`）。

Context のトップレベルは次の固定キーである。

| キー | 意味 | 読み書き |
| --- | --- | --- |
| `input` | ワークフロー開始時の input（不変） | 読み取り専用 |
| `output` | ワークフロー終端 output（実行中は空オブジェクト） | 読み取り専用（Engine のみ更新） |
| `states` | 完了済み State 名 → `{ output: … }`（履歴・監査） | 読み取り専用（Engine のみ更新） |
| `vars` | 現在の共有コンテキスト | 読み書き可（`output: $.vars…` で代入） |
| `sys` | Engine 保証のランタイム情報 | 読み取り専用 |

#### `$.sys` キー（本実装）

| パス | 値 |
| --- | --- |
| `$.sys.now` | ホスト TZ の現在時刻（ISO-8601） |
| `$.sys.today` | ホストローカル日付 `yyyy-MM-dd` |
| `$.sys.utcNow` | UTC 現在時刻（ISO-8601） |
| `$.sys.execution.id` | 実行インスタンス ID |
| `$.sys.definition.name` | コンパイル済み定義名 |

備考:

- `$.sys.state` / `$.sys.definition.id` / `version` は未提供
- 書式引数付き関数呼び出し（例: `$.sys.now("yyyyMMdd")`）は未対応
- `now` / `today` はホスト TZ 依存（コンテナでは UTC になり得る）
- 条件遷移の `when.path` は当該 State の **output** が評価根、`when.value` はリテラル。`$.sys…` を `when` で参照する契約は未対応（将来の契約変更候補。別 Spec は設けない）

#### `output` フィールド（States / Nodes）

State / action ノード完了時に、action 戻り値を `$.vars` へ代入する。

- 許可: `$.vars` または `$.vars.<seg>…`（SimpleJsonPath）
- 禁止: `$.sys…` / `$.states…` / `$.input…` 等（Level1 error）
- 未指定: `$.states` のみ更新し、`$.vars` は変更しない
- `output` 指定時も `$.states.<Name>.output` への記録は常に行う

```yaml
workflow:
  name: VarsSample

states:
  GetUser:
    action: user.get
    output: $.vars.user
    input:
      id: $.input.userId
    on:
      Completed:
        next: Notify

  Notify:
    input:
      email: $.vars.user.email
      when: $.sys.today
    on:
      Completed:
        end: true
```

`path` の単一ショートハンドと、複数キーのマップ形式をサポートする。
`input` 未定義の状態には、従来どおり直前の候補 input（直前 output / Join 辞書など）がそのまま渡る。

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
      path: $.states.Start.output.payload.value
    on:
      Completed:
        next: UseStartInput

  UseStartInput:
    input:
      orderId: $.input.orderId
    on:
      Completed:
        next: End

  End:
    on:
      Completed:
        end: true
```

- 開始 input が `{ orderId: "ORD-1" }` で、`Start` の output が `{ payload: { value: 42 } }` のとき、
  `ExtractPayload` の input は `42`、`UseStartInput` の input は `{ orderId: "ORD-1" }` になる。
- パスが見つからない、または未完了 State を参照した場合、値は `null`（警告あり）。
- `input` 定義がある状態では、定義で構築した値のみが input になる（未指定フィールドの自動マージはしない）。
- 旧来の「直前候補 input を `$` 根とする」記法（例: `path: $.payload`）は **廃止**（移行ガイドなし）。

### 1.6.1 input マップ形式（複数/ネスト/リテラル）

```yaml
states:
  B:
    input:
      foo: $.states.A.output.a
      foo.bar: $.states.A.output.a.b
      title: "my song"
      retry: 2
      enabled: true
      note: null
```

- `$` または `$.` で始まる文字列はパス式（評価根は Execution Context）
- それ以外はリテラル
- `foo.bar` はネストオブジェクトとして構築される（`StateInputEvaluator.SetByDottedKey` と同等）
- ネスト map（`ship: { address: "x" }`）とドットキー（`ship.address: "x"`）は実行時・Compiler schema 検証のいずれでも同等の論理ツリーになる（§1.1.2）
- 同一ブランチでスカラーとドットキー子が競合する正規化は 422
- パス式は `Level1Validator` と同じ単純 JSONPath 制約に従う。
  - 識別子セグメント: `$` または `$.seg1.seg2`（英数字と `_`）
  - **ドットを含む State / Node 名**はブラケット＋引用: `$.states['order.notify.customer'].output`
  - 単一引用・二重引用のどちらも可。キーが空、または引用なしブラケットは無効
- `$.vars` / `$.sys`（およびその配下）は `input` パスとして Level1 で**許可**する。
- 状態の `output` は `$.vars` 配下のみ Level1 で許可する（上記「`output` フィールド」参照）。
- `${...}` 形式のテンプレート文字列は受理しない（states/nodes ともに同一ルール）。

### 1.6.2 ユーザー定義状態（IState）との関係

- 各状態の output は `IState<TInput, TOutput>.ExecuteAsync(...)` の戻り値。
- 次状態 input の決定ルール:
  - `input` 未定義: 直前の候補 input（直前 output / Join 辞書など）をそのまま渡す
  - `input` 定義あり: Execution Context 根で評価した値のみを渡す
- `input` 定義ありの場合、マッピングしない output の値は引き継がれないため、必要な値は明示的に `input` へ記述する。

### 1.7 例（Fork/Join と input）

Fork の各分岐先・Join 後の次状態でも `input.path` を適用できる。
Join 後の分岐キー直参照（旧 `path: $.A`）は廃止し、`$.states.<StateName>.output` を使う。

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
      path: $.states.Start.output.shared
    on:
      Completed:
        next: Join1

  B:
    input:
      path: $.states.Start.output.shared
    on:
      Completed:
        next: Join1

  Join1:
    join:
      all: [A, B]
    on:
      Joined:
        next: AfterJoin

  AfterJoin:
    input:
      path: $.states.A.output
    on:
      Completed:
        end: true
```

- `Start` の output が `{ shared: "fork-value" }` の場合、`A` と `B` の input はどちらも `"fork-value"`。
- `input` 未定義の Join 次状態へは、候補として `{ A: <Aのoutput>, B: <Bのoutput> }` の辞書が渡る（パス評価根ではない）。
- `AfterJoin` の `path: $.states.A.output` により、`A` の output だけを抽出して input に渡す。

---

## 2. Nodes 形式

フル構成の公式 YAML サンプルは [`docs/samples/ui-customer-order-parallel.yaml`](../samples/ui-customer-order-parallel.yaml)（`ui-` プレフィックス・ドット区切りノード ID・条件 edges・fork/join）を参照する。

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

### 2.1.1 Definition Editor（Web UI）との往復

Definition Editor は YAML 編集とグラフ編集で同じドキュメントを扱う。次は **`NodesWorkflowDefinitionLoader` が受理する情報**がエディタ経由で欠落しないよう、クライアント側で保持する。

- **workflow**: `id` / `name` / `description`。`name` が無く `id` だけの場合、ローダーはワークフロー名に `id` を使うため、エディタも表示名を同様に解決し、`id` を別フィールドとして保持する。
- **action.input**: 文字列（`$` / `$.` パスやリテラル）またはオブジェクト（キー→パス／リテラル）。グラフのノードインスペクターからも編集できる。
- **edges[].to**: 文字列、または **`{ id: "<nodeId>" }`**。パース時にエディタは常に遷移先 ID 文字列へ正規化する。
- **join.mode**: 省略可能（省略時に UI が `mode: all` を自動付与しない）。明示したときのみ `all` を保持する。

Core-API の **`GET /v1/definitions/schema/nodes`** が返すスキーマには、`workflow.description` およびノードの **`input` / `error`** プロパティが含まれる（補完・Lint の参照用）。

### 2.2 ノード型とプロパティ

| type   | 必須プロパティ     | 任意・備考 |
|--------|--------------------|------------|
| start  | next               | 開始ノード。1 つのみ。 |
| end    | —                  | 終端ノード。 |
| action | action, next       | input, error, label 等（`onError` は現行変換では使用しない）。 |
| wait   | event, next        | timeout, onTimeout は現行変換では使用しない。 |
| fork   | branches           | 2 要素以上の配列。 |
| join   | next               | mode: all 等。 |

- **start**: `next` で次ノード ID。
- **end**: `next` なし。
- **action**: `action` はアクション参照（§1.1.1。例: `mail.send`、`statevia.action.builtin.noop`）。`next` で通常遷移先、`error` で失敗時遷移先（action のみ）。`input` で入力マップ（§1.1.2）。
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

`action` ノードの `input` / `output` は **States 形式と同じ規則**とする。パスは **`$` / `$.seg1.seg2` / `$.states['dotted.id'].…`**（`Level1Validator` の単純 JSONPath 制約に準拠）。評価根は **Execution Context**。**`${input.x}` のような `${...}` テンプレは採用しない**。

`input.path` ショートハンド、またはキーごとの `$.` 参照で Context から値を抽出できる。
`output: $.vars…` で action 戻り値を共有コンテキストへ代入できる（§1.6）。

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
      path: $.states['start'].output.shared
    next: join1

  - id: b
    type: action
    action: branch.b
    input:
      path: $.states['start'].output.shared
    next: join1

  - id: join1
    type: join
    mode: all
    next: afterJoin

  - id: afterJoin
    type: action
    action: finalize
    input:
      path: $.states['a'].output
    next: end1

  - id: end1
    type: end
```

- `start` の output が `{ shared: "fork-value" }` なら `a` / `b` は `"fork-value"` を受け取る。
- `input` 未定義の Join 次状態へは候補として `{ a: <aのoutput>, b: <bのoutput> }` が渡る。
- `afterJoin` は `path: $.states['a'].output` で `a` の output を抽出する（ドット付き Node ID も同様にブラケット引用する。旧 `$.a` は廃止）。

### 2.3.2 例（Nodes 形式 + error）

`error` は action ノード限定の失敗遷移先を表す。値はノード ID 文字列（または `{ id: "<nodeId>" }`）を受け付け、states 形式では `on.Failed.next` に変換される。

```yaml
version: 1

workflow:
  id: failed-routing
  name: Failed Routing

nodes:
  - id: start
    type: start
    next: runMain

  - id: runMain
    type: action
    action: sample.run
    next: endSuccess
    error: handleFailed

  - id: handleFailed
    type: action
    action: sample.handle-failed
    next: endSuccess

  - id: endSuccess
    type: end
```

上記は次の states へ正規化される:

```yaml
states:
  runMain:
    action: sample.run
    on:
      Completed:
        next: endSuccess
      Failed:
        next: handleFailed
```

- **label**, **description**, **tags**, **ui** は UI/エディタ用。エンジンは無視してよい。
- **metadata** をルートに置く場合もエンジンは無視してよい。

### 2.4 States 形式との対応

Nodes 形式は、実行前に **states 形式の CompiledWorkflowDefinition に変換**して利用する想定。変換レイヤーが nodes の `id`/`type`/`next`/`event`/`branches`/`error` を states の `on`/`wait`/`join` にマッピングする。実装上は Core-API の `NodesWorkflowDefinitionLoader` が nodes を `WorkflowDefinition` に変換し、states 形式は `StateWorkflowDefinitionLoader` が読み込む。

- `next` / `edges` は `on.Completed` を構成する。
- `error`（action のみ）は `on.Failed.next` を構成する。

---

## 3. ルール（共通）

- 状態名（states 形式）またはノード ID（nodes 形式）は一意とする。
- 自己遷移（A → A）は禁止。
- Join の all / branches は既存の状態・ノードを参照する。
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

---

## 5. `compiledJson` デバッグ返却契約

`compiledJson`（`DefinitionCompilerService.ValidateAndCompile`）は定義のデバッグ確認用途として、少なくとも次のキーを含む。

- `name`
- `initialState`
- `transitions`
- `conditionalTransitions`
- `forkTable`
- `joinTable`
- `waitTable`
- `stateInputs`

`conditionalTransitions` は `cases/default` をコンパイルした遷移情報、`stateInputs` は `states.<name>.input` のコンパイル済み情報を表す。

JSON キー命名は **camelCase** とする。

## 6. 関連仕様への参照

- 実行グラフ JSON（`ExportExecutionGraph`）および `conditionRouting` の詳細は `docs/specifications/execution/execution-graph.md` を正とする。
- API/UI 境界での `conditionRouting` の透過返却は `docs/specifications/api-http.md` を参照する。
- 将来の実行時データ参照（開始 input / 各 State output のパス解決）は**未実装**（拡張予定）。
