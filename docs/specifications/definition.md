# 定義仕様

| 項目 | 値 |
| --- | --- |
| 種別 | Specification |
| Version | 1.5.8 |
| 更新日 | 2026-09-02 |
| 関連 | [concepts/definition.md](../concepts/definition.md), [execution/wait-cancel.md](execution/wait-cancel.md), [execution/fork-join.md](execution/fork-join.md) |

---

## Normative 要約

- **MUST**: Fork の各枝は **Fork（Start）–任意ノード–Join（End）の単一 Definition** である。兄弟枝 Body は互いに素。領域外への出入は拒否する。
- **MUST**: Join 供給は `next` / `wait.events` / `edges` の **1 経路**で足りる。`error` / `on.Failed` は供給に数えない。
- **MUST**: publish 時に参照する `action` ID は Catalog に存在すること。未登録はエラー。
- **MUST**: `wait` または `join` を持つ状態に `action` を併記してはならない。
- **MUST**: Wait の正本は `wait.events`（イベント名 → 遷移先）。`events` は非空で、遷移先が定義内に存在すること。
- **MUST**: 同一 Wait 内のイベント名重複、予約語（`Completed` / `Failed` / `Cancelled` / `Joined`）、自己遷移を拒否する（422）。
- **MUST**: module alias は大文字小文字を区別せずテナント内で一意であること。
- **SHOULD**: 定義は states 形式で記述し、nodes 形式は UI 編集後に正規化する。
- **禁止**: 実行中に定義版を上書きすること（新版は append のみ）。詳細は [data-integration.md](data-integration.md)。
- **禁止**: 公開 DSL / HTTP / UI に `exit` / `exits` 語彙を出さない（`events` / `allowedEvents` / `resumeKey` を使う）。

背景・動機は [Concept: 定義](../concepts/definition.md) を参照。

**Version 1.5.8（2026-09-02）**: `http.request` の URL はホスト名の DNS 解決後 IP も拒否する。

**Version 1.5.7（2026-09-01）**: TrustLevel と実行モードを現行 Policy に合わせる。

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
    action: <ActionId>            # 任意。Service API の Action Registry に登録された ID（例: order.create）
    on:                          # 事実駆動の遷移（Fact → 遷移）
      <Fact>:
        next: <StateName>        # 単一遷移
        fork: [<StateName>, ...]  # 並列開始
        end: true                 # ワークフロー終了
    wait:                         # 待機（オプション）
      events:                     # 正本: イベント名 → 遷移先
        <EventName>: <StateName>
    join:                         # 合流（オプション）
      all: [<StateName>, ...]
```

- **workflow.name**: ワークフロー名（任意、デフォルト "Unnamed"）。
- **workflow.modules**: 任意。module alias（キー）→ ModuleId（値）のマップ。`action: mail.send` のように alias 付き action 参照を解決する。alias は **大文字小文字を区別せず一意**（重複・空キー・空 ModuleId は Loader 構文エラー）。現状 ModuleId に version 指定はできない（同一 moduleId の複数版共存・版レンジ解決は**未実装**）。
- **states**: 状態名 → 状態定義のマップ。各状態は `on`（遷移）、`wait`（待機）、`join`（合流）のいずれかまたは組み合わせを持つ。
- **action**: 任意。定義登録時に Service API が Catalog へ照合し、未登録の ID はエラーになる。**省略時**は implicit noop（canonical: `statevia.action.builtin.execution.noop`、即時完了）と同等。**FQCN** または `workflow.modules` の alias 参照のみを記述する（§1.1.1）。短名（`noop` / `sleep` / `rest` 等）は受理しない。**`wait` または `join` を指定する状態では `action` と併記できない**。

### 1.1.1 Action ID（canonical 形式と解決）

YAML 上の `action` 参照は syntax parse のあと、Service API Compiler（`ActionNameResolver`）で **canonical actionId** に正規化され、Action Registry へ照合される。Loader は生の参照文字列を保持し、semantic resolution は Compiler のみが行う。

| 記法（YAML） | 正規化後（canonical） | 例 |
| --- | --- | --- |
| 省略 | `statevia.action.builtin.execution.noop` | action 未指定 |
| FQCN（Builtin / Module） | そのまま | `statevia.action.builtin.execution.sleep` |
| `{alias}.{actionName}` + `workflow.modules` | `{ModuleId}.{actionName}` | `http.request` + `http: statevia.action.reference.http` → `statevia.action.reference.http.request` |
| 多段 FQCN（alias 未登録） | そのまま | `com.vendor.pkg.actionName` |
| ドットなし短名 | **エラー（HTTP 422）** | `sleep` / `rest` / `noop` |

残す Builtin（canonical）:

| Action | canonical ID | 概要 |
| --- | --- | --- |
| execution.noop | `statevia.action.builtin.execution.noop` | 即時完了（入力をそのまま出力）。省略時もこれ |
| execution.sleep | `statevia.action.builtin.execution.sleep` | `input.duration` で待機 |
| execution.signal | `statevia.action.builtin.execution.signal` | 実行スコープ内シグナル発行 |
| event.publish | `statevia.action.builtin.event.publish` | システムトピック発行（`POST /v1/events` と同じ購読照合） |
| workflow.invoke | `statevia.action.builtin.workflow.invoke` | 子ワークフロー起動（開始直後に status を返す。子終端は親 wait を自動再開しない） |

Builtin の Catalog `ModuleId` は `statevia.action.builtin`。旧 canonical（例: `statevia.action.builtin.sleep`）と短名は解決しない。

first-party リファレンス Module（ソースは `modules/reference/`。API ビルド時に `modules/default/` へ copy。未署名は Community）:

| Action | ModuleId | canonical ID |
| --- | --- | --- |
| HTTPS リクエスト | `statevia.action.reference.http` | `statevia.action.reference.http.request` |
| email 通知 | `statevia.action.reference.notification` | `statevia.action.reference.notification.send` |

テナントへ載せるには当該 Module の配置が必要。Builtin へは落とさない。alias 例:

```yaml
workflow:
  modules:
    http: statevia.action.reference.http
states:
  Call:
    action: http.request
```

廃止:

- **`delay5s`** は削除済み。`execution.sleep` + `input.duration`（例: `5s`）へ移行する。
- **短名**（`noop` / `sleep` / `rest` / `notify` / `signal` / `publish` / `workflow`）は削除済み。

解決に失敗した参照（短名・未登録 module alias・一段のみの未知 alias・Catalog 未登録 ID）はコンパイルエラー（HTTP 422）となる。

**TrustLevel と実行モード:** Catalog の TrustLevel と実行環境（`ASPNETCORE_ENVIRONMENT` / `Statevia:ExecutionPolicy:DeploymentProfile`）から実効モードを決める。Builtin（Trusted）は InProcess。Verified は Development では InProcess、それ以外は OutOfProcess。Signed / Community は OutOfProcess。Untrusted は DeploymentProfile が `saas-shared` のとき Container、それ以外は OutOfProcess。OutOfProcess は Action Host（`Statevia:ActionHost:BaseUrl`）へ送る。未設定のときは失敗する（安全側）。Policy は TrustLevel 下限を緩和できない。詳細は [action-host.md](../guides/action-host.md)。

### 1.1.2 Action パラメータと `input` キー

Action の実行パラメータ（例: rest の `url`）および状態への入力マッピングは、いずれも YAML キー **`input:`** で記述する。**`config:` キーは採用しない**（将来導入予定なし）。

- **状態 input マッピング**: 直前状態の output から値を抽出する（§1.6）。`input.path` 単一形式またはキー付きマップ。
- **Action パラメータ**: Builtin / Module action の schema に従う `input` マップ。Service API Compiler が publication の `inputSchema` で検証する（422 `details`: `state`, `actionId`, `jsonPath` — 機微値は含めない）。**Module 由来 action**（`Visibility != Builtin`）は `ActionPublication` 未登録で **422**。Builtin カスタム登録のみ structured warning（`RequireSchemaForUnpublishedActions` で strict 化可）。
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
    action: statevia.action.reference.http.request
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

### 1.1.4 Builtin / リファレンス action の input / output（MVP）

各 Builtin の `input` は §1.1.2 の action パラメータとして状態 YAML の `input:` に記述する。`output` は状態完了時に Engine が次状態へ渡す候補 input の元になる（§1.6）。rest レスポンス body、notify 本文、子 workflow の input 等はログ・一覧 GET に載せない（[io-log-masking.md](platform/io-log-masking.md)）。

#### noop（`statevia.action.builtin.execution.noop`）

- **input**: 任意（状態 input マッピングの結果をそのまま受け取る）
- **output**: 入力を **そのまま**返す（pass-through）

#### sleep（`statevia.action.builtin.execution.sleep`）

- **input**: `{ duration }` — 必須。`5s` / `500ms` / 数値（ミリ秒）を受理
- **output**: 意味のない完了（`Unit`）。**直前 payload は次状態へ引き継がれない**（pass-through しない）

#### http.request（`statevia.action.reference.http.request`）— リファレンス Module

- **input**:

  | フィールド | 必須 | 説明 |
  | --- | --- | --- |
  | `url` | はい | **HTTPS** の絶対 URL。SSRF 防止のため loopback / プライベート IP / `localhost` 等に加え、ホスト名の DNS 解決後アドレスも拒否。失敗・タイムアウト（2 秒）も拒否 |
  | `method` | はい | HTTP メソッド（例: `GET`, `POST`） |
  | `headers` | いいえ | 文字列値の map → リクエストヘッダ |
  | `body` | いいえ | 文字列または JSON オブジェクト/配列。上限 **1 MiB** |
  | `timeout` | いいえ | 秒（整数）。省略時 **30** |
  | `idempotencyKey` | いいえ | 指定時 `Idempotency-Key` ヘッダに送出 |

- **output**: `{ statusCode, headers, body }` — `body` は文字列（上限 1 MiB で切り詰め）

#### notification.send（`statevia.action.reference.notification.send`）— リファレンス Module

- **input**:

  | フィールド | 必須 | 説明 |
  | --- | --- | --- |
  | `channel` | はい | MVP は `email` のみ |
  | `to` | はい | 宛先 |
  | `subject` | はい | 件名 |
  | `body` | はい | 本文 |
  | `from` | いいえ | 差出人。省略時はプラットフォーム既定 |

- **output**: `{ channel, messageId? }`
- **接続設定（SMTP 等）**: workflow `input` には含めない。モジュールが環境変数 `STATEVIA_SMTP_HOST` / `STATEVIA_SMTP_PORT` / `STATEVIA_SMTP_USER` / `STATEVIA_SMTP_PASSWORD` / `STATEVIA_SMTP_FROM` を読む。ホストの `Infrastructure.Notification` には依存しない。未署名リファレンスの TrustLevel は Community。

#### signal（`statevia.action.builtin.execution.signal`）

- **input**: `{ signal, target? }` — `signal` 必須。`target` は MVP で `current` のみ（省略時 `current`）
- **output**: 意味のない完了（`Unit`）。同一実行内の wait 再開用に `IEventProvider.Signal` を発行する（許可イベント名と一致させる）

#### publish（`statevia.action.builtin.event.publish`）

- **input**: `{ topic, key?, payload? }` — `topic` 必須。`key` は任意（省略・空白は `""`）。`payload` は任意（この段階では Wait 再開値に反映しない）
- **output**: `{ topic, dispatched: true }` — 一致購読が 0 件でも成功（HTTP `POST /v1/events` の 204 と同じ受理）
- **配送**: Application の `IEventIngressService`（HTTP events と同じ照合）。`wait.subscribe` のアクティブ購読を topic / key の厳密一致で再開する。Signal モード（`wait.events`）は対象外。外部メッセージバスには接続しない
- **制約**: 親子専用 Resume ではない。一致が複数なら 1:N。短名 `publish` は受理しない

#### workflow（`statevia.action.builtin.workflow.invoke`）

- **input**:

  | フィールド | 必須 | 説明 |
  | --- | --- | --- |
  | `definitionId` | はい | 子定義 ID（display ID または UUID）。定義名は解決しない |
  | `input` | いいえ | 子ワークフロー開始 input（JSON 互換） |

- **output**: `{ workflowId, displayId, status }`（開始直後の status。通常は `Running`）
- **制約**:
  - 現在テナント内の定義・実行に限定
  - 子開始は HTTP `POST /v1/executions` と同じ Live Principal 経路を使わない。親実行（Action を実行中の `executions` 行。Hosted Fork 枝ならその枝）の `security_snapshot_json` を継承し、子定義の project 文脈だけ再評価する
  - 開始は常に非同期。親 Action は子の終端を待たず、開始直後の `{ workflowId, displayId, status }` を返す。子終端は親 wait を自動再開しない（Hosted Fork の `statevia.event.child.completed` とは別）。**子→親を再開する専用 Action（`execution.resume` 等）は無い。** Signal モード（`wait.events`）の正本は人手 / HTTP Resume。Subscribe モードなら既存の topic / key 発行で進められるが、公式の親子契約ではなく、一致購読が複数なら 1:N になり得る（抑止は作者の key 設計）
  - 子 `definitionId` が、実行中の定義または祖先定義の UUID と一致する場合は開始しない（実行時判定。定義保存では検証しない）

nodes 形式の定義サンプルは [`docs/samples/ui-nested-workflow-parent.yaml`](../samples/ui-nested-workflow-parent.yaml) と [`docs/samples/ui-nested-workflow-child.yaml`](../samples/ui-nested-workflow-child.yaml)。states 形式の骨子は次のとおり。

```yaml
workflow:
  name: NestedWorkflowParentSample

states:
  StartChild:
    action: statevia.action.builtin.workflow.invoke
    input:
      definitionId: $.input.childDefinitionId
    on:
      Completed:
        next: WaitChild
      Failed:
        next: StartFailed
  WaitChild:
    wait:
      events:
        ChildCompleted: AfterOk
        ChildFailed: AfterFail
  AfterOk:
    on:
      Completed:
        next: End
  AfterFail:
    on:
      Completed:
        next: End
  StartFailed:
    on:
      Completed:
        next: End
  End:
    on:
      Completed:
        end: true
```

#### Execution Semantics（signal / publish / wait）

| 概念 | スコープ | Builtin / ノード |
| --- | --- | --- |
| signal | execution-scoped | `signal` action → wait の許可イベント再開 |
| publish | system-scoped | `event.publish` → `IEventIngressService`（`POST /v1/events` と同じ） |
| wait | execution-scoped（ノード単位） | `wait.events` は `ResumeWaitNode`（HTTP Resume）。`wait.subscribe` は `POST /v1/events`（1:N ありうる） |

Phase 2（未実装）: wait ノード直下に `duration` / `signal` / `event` を排他指定する統合構文。

### 1.2 遷移（on）

- **on**: 事実名（例: `Completed`, `Joined`, `Failed`）をキーに、遷移先を指定する。
  - **next**: 次に遷移する状態名（1 つ）。
  - **fork**: 並列に開始する状態名のリスト。
  - **end**: `true` でワークフロー終了。

### 1.3 Wait（待機）

Wait は **Signal**（`events`）と **Subscribe**（`subscribe`）の二モードで、**同時指定不可**（どちらか一方が必須）。

- **wait.events**（Signal）: マップ形式。キーが受付イベント名、値が遷移先状態名。
  - コンパイル後は **`WaitEventRouteTable`**（compiled JSON: `waitEventRouteTable`）になる。
  - 再開の HTTP 正本は `POST …/nodes/{nodeId}/resume` で **`resumeKey` = イベント名**（Engine: `ResumeWaitNode`）。
  - Join 互換のため Wait 完了時の FSM 事実は **`Completed`** のまま。次状態の解決は route table（イベント名）が行う。詳細は [execution/wait-cancel.md](execution/wait-cancel.md) / [execution/fsm.md](execution/fsm.md)。
- **wait.subscribe**（Subscribe）: 配列。各要素は `topic`（必須）・`key`（任意）・`next`（必須）。
  - 集合配送用。`POST /v1/events` は topic / key のみ送り、遷移名は配信者が指定しない。
  - コンパイル時に内部イベント名 `statevia.event.subscribe.{index}` を払い出し、route table のキー → `next` に載せる。
  - 同じ index の購読は compiled JSON の **`waitSubscriptions`**（topic / key / resumeEventName / next）にも載る。実行時の Wait 実行器はこの内部イベント名を待つ。古い `compiled_json` に当該キーが無くても、同一版の `source_yaml` から再構築する。
  - `key` 省略・空白は `""` に正規化する（照合も厳密一致）。
  - 入れ子の子から親 wait を進める **専用機能ではない**。同じ topic / key を発行すれば相関はできるが、一致した購読はすべて再開し得る。1:1 に近づけたい場合の key 一意性は作者の責務。発行口は `POST /v1/events` と builtin `event.publish`（同じ `IEventIngressService`）。
- **旧形式の正規化**: `wait.event: X` + `on.Completed.next: Y` は Loader が `events: { X: Y }` へ自動変換する。`wait.events` と `wait.event` の併記はエラー。
- **公開語彙**: `events` / `subscribe` / `topic` / `key` / `next` / `allowedEvents` / `resumeKey`。`exit` / `exits` は受理しない。

### 1.4 Join（合流）

- **join.all**: 完了を待つ状態名のリスト。すべてが完了すると `Joined` 事実が発生し、`on.Joined` で次へ遷移できる。
- Join 状態は合成ノードとして実行され、**`action` は指定しない**（`wait` と同様に `action` 併記は Level 1 検証エラー）。
- **Hosted Runtime**: Fork は物理子 execution に展開される。定義語彙（`fork` / `join.all` / `$.states…`）は維持する。実行時契約（兄弟参照不可・Join 後の親 Context マージ・読みモデル合成）は [execution/fork-join.md](execution/fork-join.md) を正とする。
- **兄弟参照**: Join 前に同一 Fork の兄弟の `$.states…` / `$.vars…` を参照してはならない。Join 後は親へマージされた結果のみ参照できる（キー衝突は後勝ち）。

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
      events:
        UserApproved: Join1

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

#### `$.sys` 関数呼び出し（許可リスト）

SimpleJsonPath のプロパティ参照に加え、次の**完全一致**の関数呼び出しを `input` パス（および同じ評価根の読み取りパス）で許可する。

| 式 | 意味 |
| --- | --- |
| `$.sys.now("format")` | ホストローカル時刻を .NET カスタム日時書式で整形 |
| `$.sys.utcNow("format")` | UTC 時刻を同書式で整形 |

対比:

- `$.sys.now` → ISO-8601（引数なしプロパティ）
- `$.sys.now("yyyyMMdd")` → 例: `20260726`（8 桁）

制約:

- `format` は二重引用符で囲む（1〜64 文字）。単引用符のみ・空呼び出し・複数引数は Level1 error
- `IFormatProvider` は `CultureInfo.InvariantCulture` 固定
- 書式の意味妥当性は実行時（無効なら値 `null` + 警告）。Level1 は構文・許可リスト・長さのみ
- **未対応**: `$.sys.today("…")`、未登録関数、呼び出し後のドット連結（例: `$.sys.now("yyyy").length`）
- **`output` には使用不可**（`output` は `$.vars…` のみ）

備考:

- `$.sys.state` / `$.sys.definition.id` / `version` は未提供
- `now` / `today` はホスト TZ 依存（コンテナでは UTC になり得る）
- 条件遷移の `when.path` の評価根は **Execution Context**（`input` と同じ）。例: `$.states.A.output.x` / `$.states['a.b'].output.y` / `$.vars.flag` / `$.sys.today` / `$.sys.now("yyyyMMdd")`。`when.value` はリテラル（パス解決しない）
- State output を根とする旧 `when.path: $.x` は廃止（互換なし）

#### `output` フィールド（States / Nodes）

State / action ノード完了時に、action 戻り値を `$.vars` へ代入する。

- 許可: `$.vars` または `$.vars.<seg>…`（SimpleJsonPath の識別子／引用キー）
- 禁止: `$.sys…` / `$.sys.now("…")` 等の CallPath / `$.states…` / `$.input…` / 配列インデックス付き（例: `$.vars.users[0]`）等（Level1 error）
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
- パス式は `Level1Validator` と同じ単純 JSONPath（SimpleJsonPath）制約に従う。

| 構文 | 意味 | 例 |
| --- | --- | --- |
| `$.seg1.seg2` | オブジェクトの識別子プロパティ（英数字と `_`） | `$.vars.user.email` |
| `$.obj['key']` / `$.obj["key"]` | オブジェクトの文字列キー（ドット可） | `$.states['order.notify.customer'].output` |
| `$.arr[0]` | 配列の 0 始まりインデックス（読み取り） | `$.vars.users[0].email` |

制約・区別:

- 単一引用・二重引用のどちらも可。キーが空は無効
- **`[0]`（引用なし）≠ `['0']`（キー `"0"`）**。前者は配列インデックス、後者はオブジェクトキー
- 先頭ゼロは `0` のみ許可（`[01]` は Level1 error）。桁上限 10、`int` 範囲内
- **未サポート**（Level1 error）: `[*]` / `[-1]` / `[1:3]` / `[?()]` / `[ 0 ]`（空白あり）等
- **`output` への配列インデックスは未対応**（Level1 error。書き込みは別検討）
- `$.vars` / `$.sys`（およびその配下）は `input` パスとして Level1 で**許可**する
- 状態の `output` は `$.vars` 配下の識別子／引用キーのみ Level1 で許可する（上記「`output` フィールド」参照）
- `${...}` 形式のテンプレート文字列は受理しない（states/nodes ともに同一ルール）

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

フル構成の公式 YAML サンプルは [`docs/samples/ui-customer-order-parallel.yaml`](../samples/ui-customer-order-parallel.yaml)（`ui-` プレフィックス・ドット区切りノード名・条件 edges・fork/join）を参照する。

### 2.1 基本構造

ルートに **nodes** 配列があり、各要素が `name` と `type` を持つ。`workflow` メタデータと任意で `controls`（cancel/resume イベント）を併記する。

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
  - name: <nodeName>
    type: start | end | action | wait | fork | join
    label: <string>        # 任意
    # 型ごとのプロパティ（下記）
```

### 2.1.1 Definition Editor（Web UI）との往復

Definition Editor は YAML 編集とグラフ編集で同じドキュメントを扱う。次は **`NodesWorkflowDefinitionLoader` が受理する情報**がエディタ経由で欠落しないよう、クライアント側で保持する。

- **workflow**: `id` / `name` / `description`。`name` が無く `id` だけの場合、ローダーはワークフロー名に `id` を使うため、エディタも表示名を同様に解決し、`id` を別フィールドとして保持する。
- **action.input**: 文字列（`$` / `$.` パスやリテラル）またはオブジェクト（キー→パス／リテラル）。グラフのノードインスペクターからも編集できる。
- **wait.events**: グラフの Wait ノードインスペクタでイベント行（イベント名 → 遷移先ノード名）を追加・変更・削除できる。新規 Wait は `events` 形式を既定とする。旧形式（`event` + `next` / `edges`）は閲覧・単一 event 編集と、明示的な `events` への変換を維持する。
- **edges[].to**: 文字列、または **`{ name: "<nodeName>" }`**。パース時にエディタは常に遷移先ノード名文字列へ正規化する。
- **join.mode**: 省略可能（省略時に UI が `mode: all` を自動付与しない）。明示したときのみ `all` を保持する。

Service API の **`GET /v1/definitions/schema/nodes`** が返すスキーマには、`workflow.description` およびノードの **`input` / `error`** プロパティが含まれる（補完・Lint の参照用）。

### 2.2 ノード型とプロパティ

| type   | 必須プロパティ     | 任意・備考 |
|--------|--------------------|------------|
| start  | next               | 開始ノード。1 つのみ。 |
| end    | —                  | 終端ノード。 |
| action | action, next       | input, error, label 等（`onError` は現行変換では使用しない）。 |
| wait   | `events`（1 件以上） | 旧 `event`+`next` は Loader が `events` へ正規化。timeout / onTimeout は現行変換では未使用。 |
| fork   | branches           | 2 要素以上の配列。 |
| join   | next               | mode: all 等。 |

- **start**: `next` で次ノード名。
- **end**: `next` なし。
- **action**: `action` はアクション参照（§1.1.1。例: `mail.send`、`statevia.action.builtin.execution.noop`）。`next` で通常遷移先、`error` で失敗時遷移先（action のみ）。`input` で入力マップ（§1.1.2）。
- **wait**: 正本は **`events`**（イベント名 → 次ノード名）。単一イベントの旧形式 `event` + `next` も受理し、Loader が `events` へ正規化する。`timeout`（ISO 8601 duration）は現行変換では未使用。
- **fork**: `branches` に並列ブランチのノード名の配列。
- **join**: すべてのブランチの完了を待ち、`next` へ進む。
  - Join と Fork の対応: 各枝先頭が当該 Join を**供給**する一意の Fork を選ぶ。供給は **1 経路**で足り、次を含む。(1) 枝の `next` が直接 Join。(2) `wait.events` または `edges` を辿って Join に着く。(3) 枝先頭または枝内の内側 Fork で、その内側 Join の出口が当該 Join に到達する（ネスト。出口が他 Join のときは、その Join とペアの Fork が一段外側への供給を担う。3 段以上も各段が直近のペアだけを見る）。`error` は供給に数えない。`Join.all` は外側 Fork の枝先頭集合になる。例: [`docs/samples/ui-nested-fork.yaml`](../samples/ui-nested-fork.yaml)。Fork 再到達（循環）の例: [`docs/samples/ui-cyclic-fork.yaml`](../samples/ui-cyclic-fork.yaml)。
  - 枝 Body は互いに素。Join 前の兄弟 `$.states…` 参照、領域外への出入（`wait.events` の一部が領域外を含む場合を含む）は拒否する。`error` は枝内または当該 Join へ戻す（領域外へ出してはならない）。

### 2.3 例（Nodes 形式・抜粋）

```yaml
version: 1

workflow:
  id: order-workflow
  name: Order Processing Workflow

nodes:
  - name: start
    type: start
    label: Start
    next: createOrder

  - name: createOrder
    type: action
    label: Create Order
    action: order.create
    next: waitPayment

  - name: waitPayment
    type: wait
    label: Wait Payment
    events:
      payment.completed: endSuccess

  - name: forkFulfillment
    type: fork
    label: Parallel Fulfillment
    branches:
      - prepareShipment
      - notifyUser

  - name: joinFulfillment
    type: join
    label: Join Fulfillment
    mode: all
    next: shipOrder

  - name: endSuccess
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
  - name: start
    type: action
    action: seed
    next: fork1

  - name: fork1
    type: fork
    branches: [a, b]

  - name: a
    type: action
    action: branch.a
    input:
      path: $.states['start'].output.shared
    next: join1

  - name: b
    type: action
    action: branch.b
    input:
      path: $.states['start'].output.shared
    next: join1

  - name: join1
    type: join
    mode: all
    next: afterJoin

  - name: afterJoin
    type: action
    action: finalize
    input:
      path: $.states['a'].output
    next: end1

  - name: end1
    type: end
```

- `start` の output が `{ shared: "fork-value" }` なら `a` / `b` は `"fork-value"` を受け取る。
- `input` 未定義の Join 次状態へは候補として `{ a: <aのoutput>, b: <bのoutput> }` が渡る。
- `afterJoin` は `path: $.states['a'].output` で `a` の output を抽出する（ドット付き Node ID も同様にブラケット引用する。旧 `$.a` は廃止）。

### 2.3.2 例（Nodes 形式 + error）

`error` は action ノード限定の失敗遷移先を表す。値はノード名文字列（または `{ name: "<nodeName>" }`）を受け付け、states 形式では `on.Failed.next` に変換される。

```yaml
version: 1

workflow:
  id: failed-routing
  name: Failed Routing

nodes:
  - name: start
    type: start
    next: runMain

  - name: runMain
    type: action
    action: sample.run
    next: endSuccess
    error: handleFailed

  - name: handleFailed
    type: action
    action: sample.handle-failed
    next: endSuccess

  - name: endSuccess
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

Nodes 形式は、実行前に **states 形式の CompiledWorkflowDefinition に変換**して利用する想定。変換レイヤーが nodes の `name`/`type`/`next`/`event`/`branches`/`error` を states の `on`/`wait`/`join` にマッピングする。実装上は Service API の `NodesWorkflowDefinitionLoader` が nodes を `WorkflowDefinition` に変換し、states 形式は `StateWorkflowDefinitionLoader` が読み込む。

- `next` / `edges` は `on.Completed` を構成する。
- `error`（action のみ）は `on.Failed.next` を構成する。

---

## 3. ルール（共通）

- 状態名（states 形式）またはノード名（nodes 形式の `name`）は一意とする。
- 自己遷移（A → A）は禁止。
- Join の all / branches は既存の状態・ノードを参照する。
- Fork と Join は制御構造であり、実行順序の保証範囲は実装に依存する。Hosted では [execution/fork-join.md](execution/fork-join.md) の物理子契約に従う。
- Wait は指定イベントで再開する待機を表す。

---

## 4. 検証レベル（States 形式）

エンジンが States 形式に対して行う検証。`DefinitionValidator` はフェーズ順に実行し、各ルールが安定 `code`（`RuleId`）を持つ。HTTP 422 の details はルール単位の `code` を載せる。

**Structural（旧 LEVEL 1）**

- 構文・参照整合性
- 自己遷移の禁止
- 失敗時は Reachability / ForkRegion を実行しない

**Reachability（旧 LEVEL 2）**

- 開始状態からの到達可能性
- 循環 Join の禁止
- 失敗時は ForkRegion を実行しない

**ForkRegion**

- 枝＝単一 Definition（侵入・脱出・兄弟横断・ネスト横断）
- Join 供給（`next` / `wait.events` / `edges` の 1 経路。`error` は供給に数えない）
- 禁止パターン（他枝 `$.states`、領域外 `wait.events`）
- フェーズ内は **Weight 昇順**のうえ **全件収集**

Nodes 形式は変換後の states に対して同じ検証を適用する。Loader の Join 照合も同じ供給定義（`JoinSupply`）を使う。

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
