# Core Events Specification

Version: 1.0
Project: 実行型ステートマシン
Policy: Cancel wins

---

## 0. 方針

- コアは **Command → Event** に変換し、状態は **Event のみ**で更新する（Event Sourcing/監査性を想定）
- イベントは「事実」。後から意味を変えない
- 競合は reducer で **優先順位規則**により決定（Cancel wins）
- ここにない Event type は発行禁止（互換性・監査性のため）

---

## 1. 共通スキーマ（全イベント共通）

### 1.1 EventEnvelope

- eventId: string (UUID)
- executionId: string
- type: string (下記の固定一覧)
- occurredAt: string (RFC3339)
- actor:
  - kind: "system" | "user" | "scheduler" | "external"
  - id?: string
- correlationId?: string
- causationId?: string (直前イベントのeventIdなど)
- schemaVersion: 1
- payload: object (typeごとに定義)

---

## 2. 固定イベント一覧（完全列挙）

本プロジェクトの core event は以下の **24種** に固定する。

### A. Execution Lifecycle（4）

1. EXECUTION_CREATED
2. EXECUTION_STARTED
3. EXECUTION_COMPLETED
4. EXECUTION_ARCHIVED  ※運用上の保管/クローズ（任意）

### B. Execution Termination（Cancel/Fail）（4）

5. EXECUTION_CANCEL_REQUESTED
6. EXECUTION_CANCELED
7. EXECUTION_FAIL_REQUESTED  ※「失敗確定」前の合意形成が必要な場合用（任意）
8. EXECUTION_FAILED

> 優先順位: EXECUTION_CANCELED が最強。  
> CANCEL_REQUESTED が存在する場合、以後の終端競合は Cancel を優先して確定する。

### C. Node Lifecycle（10）

9.  NODE_CREATED
10. NODE_READY
11. NODE_STARTED
12. NODE_PROGRESS_REPORTED
13. NODE_WAITING
14. NODE_RESUME_REQUESTED
15. NODE_RESUMED
16. NODE_SUCCEEDED
17. NODE_FAIL_REPORTED
18. NODE_FAILED

### D. Node Cancellation（3）

19. NODE_CANCEL_REQUESTED
20. NODE_CANCELED
21. NODE_INTERRUPT_REQUESTED  ※実行中ワーカーへの中断要求（best-effort）

### E. Graph Control（Fork/Join）（3）

22. FORK_OPENED
23. JOIN_GATE_UPDATED
24. JOIN_PASSED

---

## 3. 各イベントの payload 定義

以下は payload の最低限フィールド（追加は将来拡張で可だが、互換性維持のため破壊的変更は禁止）。

---

### 3.1 EXECUTION_CREATED

payload:

- graphId: string
- input?: object

---

### 3.2 EXECUTION_STARTED

payload:

- startedBy?: { kind: string, id?: string }

---

### 3.3 EXECUTION_COMPLETED

payload:

- result?: object

ガード:

- CANCEL_REQUESTED / CANCELED が存在するなら reducer は COMPLETED を採用しない（No-op or keep audit）

---

### 3.4 EXECUTION_ARCHIVED

payload:

- reason?: string

---

### 3.5 EXECUTION_CANCEL_REQUESTED

payload:

- reason?: string
- requestedBy?: { kind: string, id?: string }

意味:

- この時点で「Cancel wins」判定が固定される（以後の終端競合は Cancel 優先）

---

### 3.6 EXECUTION_CANCELED

payload:

- reason?: string
- canceledAt?: string (occurredAtと同一でもよい)

意味:

- Execution 終端確定（不可逆）

---

### 3.7 EXECUTION_FAIL_REQUESTED（任意）

payload:

- reason?: string
- requestedBy?: { kind: string, id?: string }

---

### 3.8 EXECUTION_FAILED

payload:

- reason?: string
- failedNodeId?: string
- error?: { code?: string, message?: string, detail?: object }

ガード:

- CANCEL_REQUESTED / CANCELED が存在するなら reducer は FAILED を ExecutionStatus として採用しない（auditとして残すのは可）

---

### 3.9 NODE_CREATED

payload:

- nodeId: string
- nodeType: string ("Task" | "Wait" | "Fork" | "Join" | "Start" | "Success" | "Failed" | "Canceled" etc.)
- meta?: object

---

### 3.10 NODE_READY

payload:

- nodeId: string

ガード:

- Execution が終端なら No-op

---

### 3.11 NODE_STARTED

payload:

- nodeId: string
- attempt: number (1..)
- workerId?: string

ガード:

- CANCEL_REQUESTED / CANCELED が存在するなら No-op 推奨

---

### 3.12 NODE_PROGRESS_REPORTED

payload:

- nodeId: string
- progress?: number (0..100)
- message?: string
- metrics?: object

---

### 3.13 NODE_WAITING

payload:

- nodeId: string
- waitKey?: string  (外部入力の識別子)
- prompt?: object    (UI提示用のヒント。コアは解釈しない)

---

### 3.14 NODE_RESUME_REQUESTED

payload:

- nodeId: string
- resumeKey?: string
- requestedBy?: { kind: string, id?: string }

ガード:

- CANCEL_REQUESTED / CANCELED が存在するなら拒否推奨（または reducer で No-op）

---

### 3.15 NODE_RESUMED

payload:

- nodeId: string

ガード:

- node が WAITING であること（それ以外は No-op）

---

### 3.16 NODE_SUCCEEDED

payload:

- nodeId: string
- output?: object

ガード:

- CANCEL_REQUESTED / CANCELED が存在するなら NodeStatus の確定は SUCCEEDED のまま残してよいが、Execution終端は CANCELED を優先

---

### 3.17 NODE_FAIL_REPORTED

payload:

- nodeId: string
- error?: { code?: string, message?: string, detail?: object }

意味:

- 失敗の兆候/報告。確定は NODE_FAILED（もしくは Execution側確定）

---

### 3.18 NODE_FAILED

payload:

- error?: { code?: string, message?: string, detail?: object }

ガード:

- CANCEL_REQUESTED / CANCELED が存在するなら Execution終端は CANCELED を優先

---

### 3.19 NODE_CANCEL_REQUESTED

payload:

- nodeId: string
- reason?: string

意味:

- node単体キャンセル要求（Executionキャンセルとは別経路）

---

### 3.20 NODE_CANCELED

payload:

- nodeId: string
- reason?: string

意味:

- nodeの終端確定（Cancel wins）

---

### 3.21 NODE_INTERRUPT_REQUESTED

payload:

- nodeId: string
- workerId?: string
- reason?: string

意味:

- 実行中処理の停止要求（best-effort）。停止が失敗してもこのイベント自体は事実として残る。

---

### 3.22 FORK_OPENED

payload:

- nodeId: string        (Fork node)
- branchIds: string[]   (branch head node ids)

意味:

- Fork によりブランチがアクティブ化された事実

---

### 3.23 JOIN_GATE_UPDATED

payload:

- nodeId: string        (Join node)
- expectedBranches: string[]
- completedBranches: string[]
- failedBranches: string[]
- canceledBranches: string[]
- policy: "ALL_SUCCESS" | "ANY_SUCCESS" | "ALL_DONE" | "CUSTOM"
- isPassable: boolean

推奨 default:

- policy = ALL_SUCCESS

ガード:

- CANCEL_REQUESTED / CANCELED が存在する場合、isPassable の真偽に関わらず Execution終端は Cancel を優先

---

### 3.24 JOIN_PASSED

payload:

- nodeId: string (Join node)

意味:

- Join 条件を満たして次へ進んだ事実

---

## 4. イベント命名規則

- REQUESTED: 意思/要求（外部入力や上位判断）
- REPORTED: 途中報告/兆候（確定前）
- *ED（過去形）: 確定イベント（reducerの最終状態を決める）

---

## 5. Cancel Wins を担保する実装上の必須条件

- reducer は以下を必ず守る:
  - CANCEL_REQUESTED が観測されたら execution.cancelRequestedAt をセット（以後保持）
  - EXECUTION_CANCELED が適用されたら ExecutionStatus は不可逆で CANCELED 固定
  - EXECUTION_COMPLETED / EXECUTION_FAILED は CANCEL_REQUESTED がある場合、ExecutionStatus を上書きしない
- コマンド層は以下を推奨:
  - Cancel受理後の Resume/Start は拒否（ユーザー体験のため）
  - ただし event log として REQUESTED を残す運用も可（監査用途）
