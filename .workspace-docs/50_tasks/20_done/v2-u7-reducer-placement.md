# U7: reducer の所在議論

**前提**（modification-plan / U1）: **Engine がイベントを公開し、API がそれを購読**する。1 トランザクションで event_store + workflow_events に INSERT し、**reducer を適用して** workflows / execution_graph_snapshots（projection）を更新する。reducer は「EventEnvelope 列を適用して ExecutionState（またはそれに相当する projection 用状態）を導出する」純粋関数である（docs/core-reducer-spec）。

このドキュメントでは **reducer の実装を Engine に置くか API に置くか** と、**EventEnvelope の型・責務の分担** を議論する。

---

## 1. 参照仕様の整理

### 1.1 docs/core-reducer-spec.md

- **入力**: ExecutionState（または初期状態）+ EventEnvelope
- **出力**: 新しい ExecutionState
- **内容**: 優先順位関数（chooseExecStatus, chooseNodeStatus）、ガード（shouldIgnoreProgressEvent）、type 別 applyEvent、normalize。**副作用なし**。Cancel wins。
- **ExecutionState**: executionId, graphId, status (ExecutionStatus), nodes (Map<nodeId, NodeState>), version, cancelRequestedAt, canceledAt 等。
- **EventEnvelope**: eventId, executionId, type, occurredAt, actor, payload, schemaVersion（docs/core-events-spec）。

### 1.2 docs/core-events-spec.md

- **24 種**のイベント type を固定。EXECUTION_CREATED, NODE_READY, EXECUTION_CANCELED 等。
- EventEnvelope は「事実」。Engine がコマンドの結果として発行する想定。

### 1.3 現行 C# Engine

- **WorkflowSnapshot**: WorkflowId, WorkflowName, ActiveStates, IsCompleted, IsCancelled, IsFailed のみ。ExecutionState や NodeState の細かい表現はない。
- **イベント**: 現行は「発生したイベント列」を返す API はない。U1 で「コマンド戻り値 + コールバックで EventEnvelope[] を渡す」と決定済み。
- **責務**: Engine は永続化を持たず、純粋なエンジンドメインのみ（modification-plan 2.3）。

### 1.4 projection の形（API/DB）

- **workflows**: id, definition_id, status, started_at, updated_at, cancel_requested 等。
- **execution_graph_snapshots**: workflow_id, graph_json, updated_at。
- これらは **ExecutionState から導出**できる（status ↔ ExecutionStatus、graph_json ↔ ExecutionState.nodes / ExecutionGraph のシリアライズ）。

---

## 2. reducer の所在の選択肢

| 案                               | reducer の所在                                                                                                                                                                                                                                                                                                          | EventEnvelope の所在                                                                                                                                | 流れ                                                                                                                                | メリット                                                                                                                                                   | デメリット                                                                                                                                                                                  |
| -------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **A: Engine に reducer を置く**  | Engine 内に reduce(state, event) を実装。Engine が EventEnvelope 型も定義。API は「イベントを INSERT したあと、必要なら Engine の reducer を呼んで状態を取得」または「Engine がコマンド戻り／コールバックで EventEnvelope[] に加え、現在の ExecutionState 相当も返す」のいずれかで projection を更新。                  | Engine                                                                                                                                              | Engine がイベントを出し、同じ Engine が reducer で状態を計算。API はその結果を workflows / execution_graph_snapshots にマッピング。 | 仕様（core-reducer-spec）と実装が Engine に集約。イベント型と reducer の整合が 1 箇所で保たれる。リプレイ・修復時も Engine の reducer を同じように使える。 | Engine が「ExecutionState 相当」の型を持つことになる。projection の列（workflows.status 等）は DB 都合だが、ExecutionState → 行のマッピングは API が持つため、Engine は DB を依然知らない。 |
| **B: API に reducer を置く**     | API（C#）が docs/core-reducer-spec に沿って reducer を実装。Engine は **EventEnvelope の形だけ**定義（または API が EventEnvelope を定義し、Engine はその形でイベントを返す契約のみ）。API が event_store から読んだ EventEnvelope[] に reducer を適用し、得た状態を workflows / execution_graph_snapshots に書き込む。 | Engine は「イベント DTO」を返す。型は Engine と API で共有（Engine が参照する型を API が使う、または逆に API が型を定義し Engine がそれを満たす）。 | Engine はイベントを出すだけ。reducer と projection 更新はすべて API。                                                               | Engine が最小限。「永続化を知らない」が明確。                                                                                                              | reducer ロジックが docs と API の二重になり、Engine のイベント型変更時に API の reducer を手動で合わせる必要がある。リプレイ用に「同じ reducer」を API が必ず持つ必要がある。               |
| **C: 契約は Engine、実装は API** | Engine は **EventEnvelope 型と event type 定数**を公開。reducer の**インターフェース**（例: `Func<ExecutionState, EventEnvelope, ExecutionState>` や `IReducer`）は Engine が定義し、**実装は API が**用意して DI で Engine に渡す、または API が「Engine からイベントを受け取り、外部で reducer を実行」する。         | Engine が EventEnvelope と ExecutionState の型を定義。reducer 実装は API 側。                                                                       | Engine が型と契約を握り、API が reducer の実装を提供。                                                                              | 型の一貫性は Engine が保ち、ロジックは API で差し替え可能。                                                                                                | Engine が ExecutionState 型を持つ必要がある。reducer を「差し替え」する必要性が v2 でどれだけあるかは不明。複雑さが増す。                                                                   |

---

## 3. EventEnvelope の責務分担

- **誰が EventEnvelope を「出す」か**: Engine。Start / CancelAsync / PublishEvent の戻り値およびコールバックで、Engine が EventEnvelope[] を生成する（U1 決定）。
- **誰が EventEnvelope を「解釈」するか**: reducer を適用する側。**reducer が Engine にあれば** Engine が解釈。**reducer が API にあれば** API が解釈。
- **event_store との対応**: U2 で event_store は EventEnvelope を正規化して格納。リプレイ時は `WHERE workflow_id = ? ORDER BY seq` で取得し、行を EventEnvelope に復元して reducer に渡す。**reducer が Engine にあれば** API は「行 → EventEnvelope 復元」だけし、reducer 呼び出しは Engine に委譲可能。**reducer が API にあれば** API が復元と reducer の両方を行う。

---

## 4. projection の更新主体

- **workflows テーブル**: status, updated_at, cancel_requested 等は、ExecutionState（またはその簡略版）から導出できる。
- **execution_graph_snapshots**: graph_json は ExecutionState.nodes およびグラフ構造をシリアライズしたもの。
- どちらの案でも **「reducer の出力 → DB 行へのマッピング」は API が行う**（EF Core で INSERT/UPDATE するため）。違いは「reducer を誰が実装するか」だけ。

---

## 5. 推奨の方向性（議論用）

- **案 A（Engine に reducer を置く）** を推奨する。
  - **理由 1**: core-reducer-spec は「Event を適用して ExecutionState を更新する」と定義しており、**イベントの意味と状態の遷移は Engine のランタイムと一体**である。Engine がどのタイミングでどのイベントを出すか（C7 懸念）も、reducer と一緒に Engine に置けば一貫して仕様化・テストできる。
  - **理由 2**: 同じイベント列から同じ projection が得られる必要がある。reducer が Engine に 1 つだけあると、リプレイ・修復・API の通常更新で**同じロジック**を必ず使える。API に reducer があると、Engine のイベント仕様変更時に API の reducer の取りこぼしが起きやすい。
  - **理由 3**: Engine は「永続化しない」だけで、「状態を計算する」ことは責務の範囲内。reducer は副作用のない純粋関数なので、DB や HTTP に依存せず Engine に置ける。
- **EventEnvelope 型**: Engine が定義し、API はそれを参照する（Engine を ProjectReference しているため）。event_store への書き込み時は API が EventEnvelope を DB 行にマッピングする（U2 の列定義に従う）。
- **ExecutionState 相当**: Engine が「reducer の出力型」として持つ。API はその型を受け取り、workflows 行・execution_graph_snapshots 行にマッピングする。Engine は workflows テーブルの存在を知らなくてよい。

---

## 6. 決定事項・オープンな論点（記入用）

### 6.1 決定事項

| 事項                      | 決定内容                                                                                                                                                      |
| ------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| reducer の所在            | Engine に置く。EventEnvelope 型と ExecutionState 相当の型も Engine が定義。API は reducer の出力を workflows / execution_graph_snapshots にマッピング（決定） |
| EventEnvelope の定義      | Engine が定義。API は Engine を参照するため同じ型を使用（決定）                                                                                               |
| projection へのマッピング | API が担当。Engine の reducer 出力 → DB 行（決定）                                                                                                            |

### 6.2 オープンな論点

- Engine の「現在の WorkflowSnapshot」と reducer 出力の ExecutionState 相当の**関係**（GetSnapshot は従来どおりメモリ上のインスタンスから返すか、reducer 出力と統一するか）。C7（Engine がどのタイミングでどの Event を出すか）の仕様化は U7 とは別タスクとして残る。

以上を、U7 reducer の所在の決定とする。
