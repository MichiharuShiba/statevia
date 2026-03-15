# データ連携契約

Version: 1.0
Project: 実行型ステートマシン
Scope: Core-Engine / Core-API / UI 間のデータ連携
Goal: 「どのデータを」「どのタイミングで」「どの形式で」連携するかを固定する

**現在の実装**: Core-Engine は `engine/`（C# ライブラリ）、Core-API は `api/`（C#）。Engine は API プロセス内で利用。UI は `/api/core/*` で API にプロキシ。以下は原則と、実装に存在する部分に沿った記述。

---

## 0. 全体像（責務の境界）

### Core-Engine（Domain Kernel・ライブラリ）

- 固定イベント・Reducer（優先順位 + normalize）・Command→Event のルール（core-engine-events-spec / core-engine-commands-spec 等で仕様化）
- **純粋ロジック**（I/O は API 層が担当）

### Core-API（C# / Integration Boundary）

- HTTP API 契約（core-api-interface.md）
- 永続化（EF Core）・Read API（v1/workflows, v1/definitions）
- UI Push（SSE/WS）は未実装

### UI（Presentation）

- Command 発行（ワークフロー開始・キャンセル・イベント）
- Read Model 表示（一覧・詳細・ExecutionGraph）

---

## 1. 連携の基本原則

1. UI は「状態を直接書き換えない」
   - UIは **Command** を送るだけ

2. UI が読むのは **Read Model**（投影）
   - UI は Event を直接読む必要はない（監査画面は例外）

3. Core は “ライブラリ” として Core-API に組み込む（現状）
   - 将来マイクロサービス化しても契約（Command / Read / Push）は同一

4. 更新は「Command → Accepted(202) → Read Model更新 → Push」
   - UI は 202 を受けたら **Read/Pushで最終状態を確認**する

---

## 2. データモデル（UIが依存してよい形）

### 2.1 Execution Read Model（UI向け正規形）

UIが依存してよいレスポンス形を固定する。

```json
{
  "executionId": "ex-1",
  "graphId": "hello",
  "status": "ACTIVE|COMPLETED|FAILED|CANCELED",
  "cancelRequestedAt": "2026-02-28T00:00:00Z|null",
  "canceledAt": "2026-02-28T00:00:00Z|null",
  "failedAt": "2026-02-28T00:00:00Z|null",
  "completedAt": "2026-02-28T00:00:00Z|null",
  "nodes": [
    {
      "nodeId": "task-1",
      "nodeType": "Task|Wait|Fork|Join|...",
      "status": "IDLE|READY|RUNNING|WAITING|SUCCEEDED|FAILED|CANCELED",
      "attempt": 1,
      "workerId": "w1|null",
      "waitKey": "approval-1|null",
      "canceledByExecution": true
    }
  ]
}
```

#### UI側の最低保証

- UIは上記のフィールドのみを前提に描画できる
- ノードやグラフのレイアウト情報（座標など）は **別のGraph定義**として扱う（後述）

---

## 3. Core-API（HTTP）: Command 送信と Read 取得

### 3.1 Command API（Write）

- すべて `202 Accepted` を基本（非同期）
- 必須ヘッダ:
  - `X-Idempotency-Key`

- 推奨ヘッダ:
  - `X-Correlation-Id`

#### 例: Cancel

`POST /v1/workflows/{id}/cancel`（現行）

Response（固定形）

```json
{
  "executionId": "ex-1",
  "command": "CancelExecution",
  "accepted": true,
  "correlationId": "c-123",
  "idempotencyKey": "..."
}
```

### 3.2 Read API（Query）

`GET /v1/workflows/{id}`（現行）

- UIは最終状態確認にこれを使う
- コマンド直後のUI更新は（Pushがない場合）このGETをポーリングしてもよい

---

## 4. Graph 定義（UI描画に必要な静的データ）

Read Model は「状態」だけなので、UIは別途「構造」を知る必要がある。

### 4.1 Graph Definition（推奨API）

将来追加するAPI（現状は固定サンプルでも可）

`GET /graphs/{graphId}`

```json
{
  "graphId": "hello",
  "nodes": [
    { "nodeId": "start", "nodeType": "Start", "label": "Start" },
    { "nodeId": "task-1", "nodeType": "Task", "label": "Task A" }
  ],
  "edges": [{ "from": "start", "to": "task-1" }],
  "ui": {
    "layout": "dagre|manual",
    "positions": { "start": { "x": 100, "y": 80 } }
  }
}
```

#### 重要

- Execution Read Model（状態）と Graph Definition（構造）は分離する
- UIは **構造×状態** を合成して描画する

---

## 5. Realtime Push（SSE/WS）: “希薄”を埋める主役

現状の実装はPushがないため「UIが変化を感じにくい」。
Pushを入れると連携が一気に強くなる。

### 5.1 推奨: SSE

- 実装が軽い（UI側も簡単）
- サーバ→クライアント方向で十分なケースが多い

`GET /executions/{executionId}/stream` (SSE) — **未実装**

### 5.2 Push イベント（UI向け）

UIが扱いやすいよう、CoreのEventをそのまま流すのではなく、
**Read Model更新通知**を送る。

#### UI-UPDATE（最小）

```json
{
  "type": "EXECUTION_UPDATED",
  "executionId": "ex-1",
  "version": 12,
  "changed": ["status", "nodes.task-1.status"],
  "at": "2026-02-28T00:00:00Z"
}
```

UIは受けたら `GET /v1/workflows/{id}` を再取得する（最小・堅牢）。

#### UI-PATCH（中級：差分で高速化）

```json
{
  "type": "EXECUTION_PATCH",
  "executionId": "ex-1",
  "version": 12,
  "patch": [
    { "op": "replace", "path": "/status", "value": "CANCELED" },
    { "op": "replace", "path": "/nodes/task-1/status", "value": "CANCELED" }
  ],
  "at": "2026-02-28T00:00:00Z"
}
```

> まずは **UI-UPDATE** だけで十分。希薄さはこれで解決する。

---

## 6. UI の更新戦略（推奨）

### Push なし（現状）

- Command送信（202）
- UIは短い間隔で GET をポーリング（例: 0.5s→1s→2s などバックオフ）
- 状態が終端または一定時間で停止

### Push あり（推奨）

- Command送信（202）
- SSEで EXECUTION_UPDATED を受信
- UIが GET で再取得（またはPATCH適用）
- 即時にUIが追従

---

## 7. エラーとUI表現（最小ルール）

- 409 Conflict: 状態競合（例: cancelRequested後のresume）
- 422: 入力不正
- 404: execution/node不在

UIは 409 を「操作できない理由」として表示できるよう、
Core-APIはエラーレスポンスに以下を入れる。

```json
{
  "error": {
    "code": "COMMAND_REJECTED",
    "message": "Execution is cancel-requested",
    "details": {
      "cancelRequestedAt": "..."
    }
  }
}
```

---

## 8. シーケンス（現状と将来）

### 8.1 現状（Pushなし）

UI -> Core-API: POST /v1/workflows/{id}/cancel
Core-API -> UI: 204 No Content
UI -> Core-API: GET /v1/workflows/{id} (poll)
Core-API -> UI: status 等

### 8.2 将来（Pushあり・未実装）

UI -> Core-API: POST /cancel
Core-API -> UI: 202 Accepted
Core-API -> UI: SSE EXECUTION_UPDATED
UI -> Core-API: GET /v1/workflows/{id}
Core-API -> UI: status=CANCELED

---

## 9. バージョニング（互換性）

- Read Model は後方互換を守る（フィールド追加は可、削除/意味変更は不可）
- Push payload も同様
- 破壊的変更が必要なら `v2` エンドポイントを追加する
