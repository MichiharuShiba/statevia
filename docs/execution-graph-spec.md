# ExecutionGraph JSON Schema

## 1. 目的

ExecutionGraph は **実行結果の事実ログ構造** であり：

- 実行の可視化
- Fork / Join / Wait / Cancel の追跡
- デバッグ / リプレイ / 検証
- UI / CLI / 外部ツール連携

のための **読み取り専用スキーマ** とする。

---

## 2. トップレベル構造

```json
{
  "executionId": "string",
  "definitionId": "string",
  "status": "Running | Paused | Completed | Failed | Cancelled",
  "startedAt": "2026-02-18T12:00:00Z",
  "endedAt": "2026-02-18T12:05:00Z",
  "nodes": [],
  "edges": [],
  "events": [],
  "meta": {}
}
```

---

## 3. Node（State 実行単位）

```json
{
  "id": "node-uuid",
  "stateId": "A",
  "statusType": "Task",
  "status": "Idle | Running | Waiting | Completed | Failed | Cancelled",
  "startedAt": "2026-02-18T12:00:01Z",
  "endedAt": "2026-02-18T12:00:05Z",
  "attempt": 1,
  "outputRef": "payload://node-uuid/output",
  "error": {
    "type": "ExceptionType",
    "message": "error message",
    "stackTraceRef": "blob://..."
  },
  "cancel": {
    "reason": "UserRequested",
    "cause": "ExternalCancelAPI",
    "requestedAt": "2026-02-18T12:00:03Z"
  },
  "meta": {}
}
```

### フィールド説明

| フィールド      | 意味                  |
| ---------- | ------------------- |
| statusType | Task / Wait         |
| status     | Node.status 最終確定6種  |
| outputRef  | Payload Snapshot 参照 |
| error      | Failed 時のみ          |
| cancel     | Cancelled 時のみ       |
| attempt    | 再実行やリトライ拡張余地        |

---

## 4. Edge（実行関係）

```json
{
  "id": "edge-uuid",
  "from": "node-uuid",
  "to": "node-uuid",
  "type": "Next | Fork | Join | Resume | Cancel",
  "event": {
    "type": "UserEvent",
    "key": "DoneC",
    "payloadRef": "payload://event/123"
  },
  "at": "2026-02-18T12:00:05Z"
}
```

### Edge.type

- Next
- Fork
- Join
- Resume
- Cancel

---

## 5. Event（外部入力ログ）

```json
{
  "id": "event-uuid",
  "type": "UserEvent",
  "key": "DoneC",
  "receivedAt": "2026-02-18T12:00:04Z",
  "payloadRef": "payload://event/123",
  "meta": {}
}
```

---

## 6. Payload Snapshot（参照のみ / redaction 対応）

ExecutionGraph 本体には Payload を含めない。

```json
payload://node-uuid/output
payload://event/123
```

### redactionPolicy

```json
{
  "payloadPolicy": "Full | Hash | MetadataOnly | None"
}
```

- Full: 完全保存
- Hash: ハッシュのみ
- MetadataOnly: 型情報 + サイズ
- None: 保存しない

---

## 7. 設計原則

- ExecutionGraph は **事実のみを記録**
- ExecutionGraph は **Engine の判断ロジックを含まない**
- ExecutionGraph は **再実行のための入力にはならない**
- Payload は参照のみ（PII / Secrets 保護）
- Cancel は外部入力の reason / cause を保持可能
- Resume は Event / 直接指定の両対応
- Wait は専用スコープで Context とは分離

---

## 8. 互換性・将来拡張

- Node.attempt により Retry / 再実行を将来拡張可能
- meta はツール・UI 拡張用の自由領域
- Edge.type / EventType は enum 拡張可能
