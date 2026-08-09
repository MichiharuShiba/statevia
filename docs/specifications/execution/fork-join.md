# Fork / Join 仕様

| 項目 | 値 |
| --- | --- |
| 種別 | Specification |
| Version | 1.1 |
| 更新日 | 2026-08-09 |
| 関連 | [fsm.md](fsm.md), [../definition.md](../definition.md), [wait-cancel.md](wait-cancel.md), [execution-graph.md](execution-graph.md), [../../concepts/durability.md](../../concepts/durability.md), [../../reference/database-schema.md](../../reference/database-schema.md) |

---

## Normative 要約

- **MUST**: Fork / Join は**制御ノード**であり、通常の状態（State）ではない。
- **MUST**: Join は依存する全分岐から所定の事実が揃うまで次へ進んではならない。
- **MUST**: **Hosted Runtime** では Fork 到達時に各分岐を**物理子 execution** として展開する（論理 Fork 互換フラグは設けない）。
- **MUST**: **Join 前**は同一 Fork の兄弟分岐同士が互いの `$.states…` / `$.vars…` / Context を参照してはならない。
- **MUST**: **Join 後（成功）**は子の `states` / `vars` を親 Execution Context へマージし、親の後続から `$.states.<Name>.output` 等を解決できること。キー衝突は子完了信号の**適用順で後勝ち**（Failed にしない）。
- **MUST**: 親の `GET …/graph` / `GET …/events` は、UI が物理分割を意識しないよう **GET 時に論理 1 実行へ合成**する（永続への二重書き込みはしない）。
- **SHOULD**: Fork は定義上で並列開始する状態集合を明示する。

---

## Fork

Fork は複数の状態を同時に開始します。

例：A -> Fork -> [B, C]

### Hosted Runtime（物理子 execution）

Hosted（Service API / Runtime Worker）では、親が Fork に到達すると Runtime が次を行う。

1. 各分岐先頭に対し子 `executionId` を作成し、親子リンクを **`execution_branches`** に永続化する。
2. 親の `ExecutionSecuritySnapshot` を子へ**継承**する（初版は再評価しない）。
3. 各子へ `Start` work item を enqueue する（`InitialState` = 分岐先頭、input は定義の写像）。
4. 親は分岐本体を自インスタンスでは走らず、Join 待ちに入る（Unload 可）。

識別の軸:

| 列 | 意味 |
| --- | --- |
| `fork_node_id` | 親実行グラフ上の Fork **到達**インスタンス（実行ノード ID）。循環再到達の区別に使う |
| `join_state` / `branch_state` | 定義の状態名空間（Join 解決・子開始状態） |
| `execution_id` | 子 execution |
| `status` | 分岐完了事実の要約（Running / Completed / Failed / Cancelled） |

スキーマ詳細は [database-schema.md](../../reference/database-schema.md)（`execution_branches`）を参照。

展開の一時失敗では親をすぐ Failed にしない。**展開リトライ**し、試行上限超過後に残子を Cancel したうえで親を **Failed** にする。展開成功後の子失敗は Join 規則に従う。

スタンドアロン Engine（Host なし）は従来の**論理 Fork**（同一 execution 上の並列）を維持してよい。

### ネスト

子の中の Fork も同一メカニズムで再帰的に物理分割する。

---

## Join

Join は複数の状態からの事実を待ってから次に進みます。

例：Join(E) = all(B, C)

### Hosted での集約

- Join 充足の正本は **`execution_branches`**（親を Engine の durable Wait に載せない）。
- 子が終端すると、親向け `Resume`（`mode=event`）に予約イベント名 **`statevia.event.child.completed`** を載せて enqueue する。
- Worker は当該予約イベントを Engine の通常 Wait Resume ではなく **Join 再評価**へ振る。
- 全必須分岐が Completed → Join 充足。いずれかが Failed / Cancelled → 親へ失敗／キャンセル伝播。

### Context マージと兄弟参照

| 時点 | 契約 |
| --- | --- |
| Join 前 | 兄弟分岐間の `$.states…` / `$.vars…` / Context 参照は**不可**。使えるのは Fork 時の親→子 input 写像のみ |
| Join 後（成功） | 各子の完了 `states` と終端 `vars` を親 Context へマージ。後続の `input.path: $.states.A.output` 等は親上で解決する。同一キー衝突は適用順の**後勝ち** |
| Join 失敗／Cancel 伝播 | 不完全な Context マージは必須としない |

定義 DSL 上の候補 input 辞書（分岐キー → 子 output）の形は従来どおり維持する。詳細の記述例は [definition.md](../definition.md) の Fork/Join と input 節を参照。

---

## ルール

- Fork と Join は状態ではありません。
- Join は依存状態が事実を生成するたびに評価されます。
- 必須状態のいずれかが失敗またはキャンセルされた場合、Join は失敗します。
- Fork は実行順序を保証しません。
- 親 Cancel は未終端の物理子へ Cancel をカスケードする（[wait-cancel.md](wait-cancel.md)）。
- 分岐上の Wait へのイベント配送は**子 execution** を正とする（親 ID への誤配送を解決または拒否する）。

---

## 読みモデル（UI は分割非認知）

クライアント／UI は物理子や `execution_branches` を意識しない。親の読み取り API が論理 1 実行に見せる。

### 実行グラフ（`GET …/graph`）

- 永続 `execution_graph_snapshots` は親／子それぞれの**生グラフ**のまま。
- 親 GET 時に論理 Fork/Join へ**合成**する。
- 合成ノードの **`workerId`（必要なら `attempt`）は当該分岐を実行した子グラフから引き継ぐ**。
- Join 辺は Join 到達の実行 `nodeId` に固定する（状態名のみでは循環再到達で曖昧）。
- 親 Fork からの Fork 辺は `branch_state` ノードへだけ張り、親 Join への Join 辺は枝先頭状態の終端に限る。ネスト内側 Join → 外側 Join は Next とし、定義にない合流枝に見えないようにする。

詳細契約は [execution-graph.md](execution-graph.md)。

### イベントタイムライン（`GET …/events`）

- 親と再帰子孫の `event_store` を **GET 時合成**する（親への二重 append はしない）。
- 並び: `occurred_at` 昇順。同刻は親（ルート）優先 → `execution_id` → 永続 `seq`。
- 応答の `seq` は**合成通番**。`afterSeq` は合成 seq 基準のページング。
- **子孫**の `WorkflowStarted` / `WorkflowCancelled` は落とす。子孫の `EventPublished` と親のマップ対象型は残す。
- `GraphUpdated` の patch は合成グラフ由来。タイムライン上の `executionId` は親の displayId。

配送（子 ID 正本）と表示合成は別関心である。
