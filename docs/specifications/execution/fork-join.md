# Fork / Join 仕様

| 項目 | 値 |
| --- | --- |
| 種別 | Specification |
| Version | 1.3.1 |
| 更新日 | 2026-08-14 |
| 関連 | [fsm.md](fsm.md), [../definition.md](../definition.md), [wait-cancel.md](wait-cancel.md), [execution-graph.md](execution-graph.md), [../../concepts/durability.md](../../concepts/durability.md), [../../reference/database-schema.md](../../reference/database-schema.md) |

---

## Normative 要約

- **MUST**: 定義時、各 Fork 枝は **Fork（Start）–任意ノード–Join（End）の単一 Definition** である。兄弟 Body は互いに素。Join 供給は `next` / `wait.events` / `edges` の 1 経路（`error` は供給に数えない）。詳細は [definition.md](../definition.md) §2.2 / §4。
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

#### 識別子: `forkNodeId`（DB: `fork_node_id`）

Hosted 物理 Fork では、**1 回の Fork 到達（並列エピソード）**を相関する正本キーが `forkNodeId` である。第三の宇宙を増やさず、親実行グラフに既に存在する **Fork 到達ノードの `nodeId`** をそのまま使う。

| 識別子 | 何を指すか | 循環での性質 |
| --- | --- | --- |
| 定義の状態名（`nodeName` / `join_state` / `branch_state`） | DSL 上の Fork / Join / 枝先頭の種類 | 周回しても同じ |
| 実行 `nodeId`（一般） | 親グラフ上の 1 訪問実体（Fork・Wait・Action 等） | 到達のたびに新規 |
| **`forkNodeId`** | **当該並列エピソードの相関キー**＝その訪問の Fork の `nodeId` | 周回ごとに別値。枝行・Join 再評価・合成の単位 |
| Join の実行 `nodeId` | Join がグラフに載ったあとの訪問実体 | Fork 展開時点では未作成になりうる |

**役割（MUST）**

- **割り当て時点**: 親 Engine が Fork ノードを実行グラフへ追加したとき。値は親グラフの Fork `nodeId`。
- **永続**: `execution_branches` の主キー構成要素 `(parent_execution_id, fork_node_id, branch_state)`。同一親・同一 Fork 訪問の兄弟枝を束ねる。
- **Join 集約**: 子完了 Resume（`statevia.event.child.completed`）は payload の `forkNodeId` で枝集合を引き、充足・失敗伝播・冪等判定の単位とする。
- **未到達 Join**: Join の `nodeId` はまだ無くてよい。相関は常に `forkNodeId` 側が持つ。Join 実行後にグラフ上の Join `nodeId` へ写像する（合成・冪等投影用）。

**役割に含めないもの**

- 定義上の Fork / Join 状態名の代替ではない（種類の識別は `join_state` / 定義名）。
- 子 execution の ID ではない（子は `execution_id`）。
- Join 未到達時の「未来の Join `nodeId`」の先貸しではない。

枝行のその他列:

| 列 | 意味 |
| --- | --- |
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
- 物理 Join 待ち中の親は Engine の durable Wait を持たないため、runtime checkpoint の内容保持は寿命表の **`PendingPhysicalJoin`**（未終端 `execution_branches`）による。`PendingWaits` とあわせた内容寿命の正本は [durability.md](../../concepts/durability.md)。Worker 所有 lease は保持理由と同列にしない。
- hydrate 入力として欠落・`{}`・必須プロパティ欠落は不正とし、構造化ログのうえ一時失敗とする（lease 用 seed `{}` を Import しない）。
- 循環で同一 Join 状態名へ戻るとき、`CompletePhysicalJoin` は前回訪問の `startedJoins` / 観測事実をクリアしてから再実行する（残ると Join が無動作のまま Unload され停滞する）。
- Join 直後の Unload で in-process 投影が欠落しても、checkpoint に Wait / 終端が進んでいれば冪等完了。進んでいなければ一時失敗して `child.completed` を再処理する。
- `CompletePhysicalJoin` 後は `ActiveStates==0` ですぐ投影しない。durable Wait 登録または Suspend Unload まで待ち、decide Wait 無しの古い graph snapshot を残さない。
- `child.completed` 再送の冪等は、当該 Fork 訪問に対応する Join ノードが既に `fact=Joined`（または `completedAt`）なら Join 再実行しない。投影の `status` だけ見ると取りこぼし、同一訪問の Join が多重実行されて `pendingWaits` が積み上がる。
- Resume（Again → Fork）後に Suspend Unload した場合、in-process 投影が欠けても SuspendHandler が書いた DB 断面を使い 500 にしない。
- 同一実行の checkpoint Persist/Unload は直列化する。加えて、永続済みに `pendingWaits` があるのに空の Fork 待ち断面で上書きしようとした Persist は拒否する（遅延 Fork Unload が decide Wait を消す競合の防止）。
- Fork 展開通知は非同期のため、`ExpandFork` 完了後の Persist が Join→decide より遅れることがある。その時点で Engine に `activeStates` / `pendingWaits` がある、または当該 Fork 以降の Join が既に `Joined` なら **Unload しない**（Wait Suspend 側の Persist に委ね、初回 Wait 断面の消失を防ぐ）。
- 定義グラフ（Studio）の Join 辺は `joinTable` 依存のうち、当該 Join へ**直接**着くものだけを描く。Fork 状態、および枝内の内側 Fork へ進む action（`mid → inner.fork` など）は除外する。ネストの描画経路は InnerJoin→OuterJoin（transitions）とし、枝先頭→OuterJoin の幽霊辺を作らない。

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
- **定義時の領域**: 枝は単一 Definition。Join 後の Again で同じ Fork に戻る循環は合法。枝内から Join 以外へ出る遷移、領域外からの侵入、兄弟 Body の共有は拒否する。`error` は枝内または当該 Join へ戻し、領域外へ出してはならない。
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
