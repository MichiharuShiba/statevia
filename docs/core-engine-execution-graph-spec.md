# ExecutionGraph 現行仕様

Version: 1.1（2026-05-11: Node 運用メタ・入力・Edge キー `from`/`to` を実装に整合）
Project: 実行型ステートマシン

---

## 1. 対象と責務

本書は、`engine/Statevia.Core.Engine/ExecutionGraph` の**現行実装**が返す JSON 契約を定義する。

- 対象出力: `WorkflowEngine.ExportExecutionGraph(workflowId)`
- 用途: 実行可視化、デバッグ、API/UI 連携
- 命名: JSON キーは **camelCase**

---

## 2. 返却の基本挙動

### 2.1 ワークフローが存在する場合

`ExportExecutionGraph(workflowId)` は次のトップレベル JSON を返す。

```json
{
  "nodes": [],
  "edges": []
}
```

### 2.2 ワークフローが存在しない場合

`workflowId` が見つからない場合は、空オブジェクト文字列を返す。

```json
{}
```

---

## 3. トップレベル構造（存在時）

| キー | 型 | 説明 |
| --- | --- | --- |
| `nodes` | `ExecutionNode[]` | 状態実行ノードの配列 |
| `edges` | `ExecutionEdge[]` | ノード間遷移の配列 |

---

## 4. Node 契約（`ExecutionNode`）

`System.Text.Json` の既定シリアライズにより、C# の `ExecutionNode` は次のキーが出力される（未設定の参照型は `null`、ブールは既定 `false`）。

```json
{
  "nodeId": "a1b2c3d4",
  "stateName": "Route",
  "nodeType": "Task",
  "startedAt": "2026-04-21T12:00:00.0000000Z",
  "completedAt": "2026-04-21T12:00:01.0000000Z",
  "fact": "Completed",
  "output": {
    "score": 5
  },
  "input": {
    "foo": "bar"
  },
  "attempt": 1,
  "workerId": "a1b2c3d4",
  "waitKey": null,
  "canceledByExecution": false,
  "conditionRouting": {
    "fact": "Completed",
    "resolution": "default_fallback",
    "matchedCaseIndex": null,
    "caseEvaluations": [],
    "evaluationErrors": []
  }
}
```

| キー | 型 | 必須 | 説明 |
| --- | --- | --- | --- |
| `nodeId` | `string` | 必須 | **ランタイム実行ノード ID**。`Guid("N")` の先頭 8 文字。定義キャンバスの `nodeId`（状態名ベース）とは別物 |
| `stateName` | `string` | 必須 | ワークフロー定義上の状態名 |
| `nodeType` | `string` | 必須 | ノード種別（例: `Start` / `Task` / `Fork` / `Join` / `Wait` / `End`。既定は `Task`） |
| `startedAt` | `string(date-time)` | 必須 | ノード開始 UTC 時刻 |
| `completedAt` | `string(date-time) \| null` | 任意 | 未完了時は `null` |
| `fact` | `string \| null` | 任意 | 完了時事実（例: `Completed`, `Failed`, `Cancelled`, `Joined`）。`Cancelled` のとき `canceledByExecution` が `true` になる |
| `output` | `any \| null` | 任意 | 状態出力。`object` としてシリアライズ |
| `input` | `any \| null` | 任意 | 当該状態実行に渡された入力（説明責任・Join 合成入力の記録に利用） |
| `attempt` | `number` | 必須 | 試行回数（現行実装では主に `1`） |
| `workerId` | `string \| null` | 任意 | ワーカー識別子。現行では未指定時に `nodeId` と同値が入ることがある |
| `waitKey` | `string \| null` | 任意 | Wait 系の待機キー |
| `canceledByExecution` | `boolean` | 必須 | 実行全体のキャンセルにより当該ノードがキャンセル扱いになったか |
| `conditionRouting` | `ConditionRoutingDiagnostics \| null` | 任意 | output 条件遷移の診断 |

補足:

- `conditionRouting` は条件遷移を評価したノードで設定される。線形遷移のみでは `null` になり得る。
- `output` / `input` は状態実装・スケジュール経路の値をそのまま保持するため、JSON 型は固定されない。外部ログへ載せる際は IO-14（`docs/core-api-interface.md` / `AGENTS.md`）に従いマスキング・サイズ制御を行う。

---

## 5. Edge 契約（`ExecutionEdge`）

JSON プロパティ名は **camelCase** のため、C# の `From` / `To` は **`from` / `to`** として出力される。値はいずれも **`nodes[*].nodeId`（ランタイム実行ノード ID）** を指す。

```json
{
  "from": "a1b2c3d4",
  "to": "f0e1d2c3",
  "type": 0
}
```

| キー | 型 | 必須 | 説明 |
| --- | --- | --- | --- |
| `from` | `string` | 必須 | 遷移元の実行ノード ID（`nodeId`） |
| `to` | `string` | 必須 | 遷移先の実行ノード ID（`nodeId`） |
| `type` | `number` | 必須 | `EdgeType` 列挙値（数値） |

`type` の値は次の対応:

- `0`: `Next`
- `1`: `Fork`
- `2`: `Join`
- `3`: `Resume`
- `4`: `Cancel`

注: 現行実装は `JsonStringEnumConverter` を使っていないため、`type` は文字列ではなく数値で出力される。

`Join`（`2`）では、複数の合流元ノードから同一の Join 合成ノードへ **`from` が異なる辺が複数本** 立つことがある（合流の可視化用）。

---

## 6. `conditionRouting` 契約

`conditionRouting` は `ConditionRoutingDiagnostics` の JSON 表現。

```json
{
  "fact": "Completed",
  "resolution": "default_fallback",
  "matchedCaseIndex": null,
  "caseEvaluations": [
    {
      "caseIndex": 0,
      "declarationIndex": 0,
      "order": 10,
      "matched": false,
      "reasonCode": "condition_false",
      "reasonDetail": null
    }
  ],
  "evaluationErrors": []
}
```

| キー | 型 | 必須 | 説明 |
| --- | --- | --- | --- |
| `fact` | `string` | 必須 | 評価した事実名 |
| `resolution` | `string` | 必須 | 解決種別 |
| `matchedCaseIndex` | `number \| null` | 任意 | 一致 case index |
| `caseEvaluations` | `ConditionCaseEvaluationRecord[]` | 必須 | 各 case の評価結果 |
| `evaluationErrors` | `string[]` | 必須 | 評価エラー/警告メッセージ |

`resolution` は次のいずれか:

- `linear`
- `matched_case`
- `default_fallback`
- `no_transition`

### 6.1 `caseEvaluations[*]` 契約

| キー | 型 | 必須 | 説明 |
| --- | --- | --- | --- |
| `caseIndex` | `number` | 必須 | `cases` 配列上の index（0 始まり） |
| `declarationIndex` | `number` | 必須 | 定義上の宣言順 |
| `order` | `number \| null` | 任意 | case の `order` |
| `matched` | `boolean` | 必須 | 条件一致したかどうか |
| `reasonCode` | `string \| null` | 任意 | 不一致理由コード |
| `reasonDetail` | `string \| null` | 任意 | 補足メッセージ |

`reasonCode` は実装上、`condition_false` / `path_not_found` / `compare_unsupported` などを返す場合がある。

---

## 7. API/UI 境界

- `GET /v1/workflows/{id}/graph` の本文は、本書の **`nodes` / `edges` 構造をそのまま** `execution_graph_snapshots` から返す（キー名・意味はエンジン `ExportJson` と一致）。
- API は実行グラフの `conditionRouting` を透過的に返却する。
- UI は `conditionRouting` を再評価しない（表示専用データとして扱う）。
- UI が定義グラフ（`GET /v1/graphs/{graphId}`）と合成するときは、**実行ノードの `nodeId` と定義ノードの `nodeId`（状態名）が一致しない**前提で、`stateName` やエッジの `from`/`to` を用いて対応付ける（`services/ui/app/lib/mergeGraph.ts`）。

詳細は `docs/core-api-interface.md` を参照。
