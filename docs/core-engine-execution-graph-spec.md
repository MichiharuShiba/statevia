# ExecutionGraph 現行仕様

Version: 1.0
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

```json
{
  "nodeId": "a1b2c3d4",
  "stateName": "Route",
  "startedAt": "2026-04-21T12:00:00.0000000Z",
  "completedAt": "2026-04-21T12:00:01.0000000Z",
  "fact": "Completed",
  "output": {
    "score": 5
  },
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
| `nodeId` | `string` | 必須 | ノード識別子。`Guid("N")` の先頭 8 文字 |
| `stateName` | `string` | 必須 | 状態名 |
| `startedAt` | `string(date-time)` | 必須 | ノード開始 UTC 時刻 |
| `completedAt` | `string(date-time) \| null` | 任意 | 未完了時は `null` |
| `fact` | `string \| null` | 任意 | 完了時事実（例: `Completed`, `Failed`, `Cancelled`, `Joined`） |
| `output` | `any \| null` | 任意 | 状態出力。`object` としてシリアライズ |
| `conditionRouting` | `ConditionRoutingDiagnostics \| null` | 任意 | output 条件遷移の診断 |

補足:

- `conditionRouting` は条件遷移を評価したノードで設定される。線形遷移のみでは `null` になり得る。
- `output` は状態実装の戻り値をそのまま保持するため、JSON 型は固定されない。

---

## 5. Edge 契約（`ExecutionEdge`）

```json
{
  "fromNodeId": "a1b2c3d4",
  "toNodeId": "f0e1d2c3",
  "type": 0
}
```

| キー | 型 | 必須 | 説明 |
| --- | --- | --- | --- |
| `fromNodeId` | `string` | 必須 | 遷移元ノード ID |
| `toNodeId` | `string` | 必須 | 遷移先ノード ID |
| `type` | `number` | 必須 | `EdgeType` 列挙値（数値） |

`type` の値は次の対応:

- `0`: `Next`
- `1`: `Fork`
- `2`: `Join`
- `3`: `Resume`
- `4`: `Cancel`

注: 現行実装は `JsonStringEnumConverter` を使っていないため、`type` は文字列ではなく数値で出力される。

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

- API は実行グラフの `conditionRouting` を透過的に返却する。
- UI は `conditionRouting` を再評価しない（表示専用データとして扱う）。

詳細は `docs/core-api-interface.md` を参照。
