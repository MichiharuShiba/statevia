# Core Commands Specification (Fixed List)

Version: 1.0
Project: 実行型ステートマシン
Policy: Cancel wins

---

## 0. 方針

- 外部入力は Command として受ける
- Command は **必ず検証（ガード）**され、通れば 1つ以上の Event に変換される
- reducer は Event だけを見る（コマンド直適用は禁止）
- Cancel wins を UX と整合させるため、Cancel要求以降は「進行系コマンド」を原則拒否

---

## 1. 固定コマンド一覧（完全列挙）

本プロジェクトの core command は以下の **12種** に固定する。

### A. Execution

1. CreateExecution
2. StartExecution
3. CancelExecution
4. ArchiveExecution

### B. Node（主にWait/Task）

5. MarkNodeReady
6. StartNode
7. ReportNodeProgress
8. PutNodeWaiting
9. RequestResumeNode
10. ResumeNode
11. SucceedNode
12. FailNode

> Fork/Join 由来の READY 化や Join通過は、Orchestrator（後述）が内部的に MarkNodeReady を発行する。

---

## 2. 共通入力（全コマンド共通）

- executionId: string
- actor: { kind: "system"|"user"|"scheduler"|"external", id?: string }
- correlationId?: string

---

## 3. ガード（共通ルール）

### 3.1 Execution終端後

- Execution.status が COMPLETED/FAILED/CANCELED の場合、**全コマンド拒否**
  - 例外: ArchiveExecution は許可してよい（運用）

### 3.2 Cancel要求以降（Cancel wins）

- execution.cancelRequestedAt が存在する場合、以下は **原則拒否**
  - StartExecution
  - MarkNodeReady
  - StartNode
  - ReportNodeProgress
  - PutNodeWaiting
  - RequestResumeNode
  - ResumeNode
  - SucceedNode
  - FailNode

許可するもの:

- CancelExecution（冪等化）
- ArchiveExecution（運用）

> 監査目的で「要求だけは記録したい」場合は、
> 拒否せず REQUESTED 系 Event を出して reducer 側で No-op でもよいが、
> デフォルトは拒否（ユーザー体験・整合性優先）。

---

## 4. Command → Event 変換表

### 4.1 CreateExecution

**Guards**  

- executionId が未使用
- graphId が存在

**Emits**  

- EXECUTION_CREATED

payload:

- graphId
- input?

---

### 4.2 StartExecution

**Guards**  

- Execution.status == ACTIVE（Created直後もACTIVE扱い）
- cancelRequestedAt == null

**Emits**  

- EXECUTION_STARTED

---

### 4.3 CancelExecution

**Guards**  

- 終端でなければ受理（冪等）
- 終端でも「すでにCanceledならOK」「それ以外終端なら拒否/No-op」運用を選べる
  - デフォルト: 終端なら No-op（Event出さない）

**Emits（推奨: 2段階）**  

1) EXECUTION_CANCEL_REQUESTED（初回のみ）
2) EXECUTION_CANCELED（確定）

> “確定”を即時にするか、ワーカー停止などを待って確定するかは運用選択。
> ただし Cancel wins のため REQUESTED が入った時点で以後の終端競合は Cancel 優先。

---

### 4.4 ArchiveExecution

**Guards**  

- 任意（運用次第）
- 通常は終端後のみ許可推奨

**Emits**  

- EXECUTION_ARCHIVED

---

### 4.5 MarkNodeReady

**Guards**  

- node.status in { IDLE }（または IDLE/READY 冪等）
- Execution 終端でない
- cancelRequestedAt == null

**Emits**  

- NODE_READY

---

### 4.6 StartNode

**Guards**  

- node.status in { READY }（冪等で READY/RUNNING も許可してよい）
- cancelRequestedAt == null

**Emits**  

- NODE_STARTED (attempt, workerId)

---

### 4.7 ReportNodeProgress

**Guards**  

- node.status in { RUNNING }（任意で WAITING も可）
- cancelRequestedAt == null（デフォルト拒否）

**Emits**  

- NODE_PROGRESS_REPORTED

---

### 4.8 PutNodeWaiting

**Guards**  

- node.status in { RUNNING }
- cancelRequestedAt == null

**Emits**  

- NODE_WAITING (waitKey, prompt?)

---

### 4.9 RequestResumeNode

**Guards**  

- node.status == WAITING
- cancelRequestedAt == null

**Emits**  

- NODE_RESUME_REQUESTED

---

### 4.10 ResumeNode

**Guards**  

- node.status == WAITING
- cancelRequestedAt == null
- (option) resumeKey が一致すること

**Emits**  

- NODE_RESUMED

---

### 4.11 SucceedNode

**Guards**  

- node.status in { RUNNING }（WAITING を許可するかは運用）
- cancelRequestedAt == null（デフォルト拒否）
- node.status が終端でない

**Emits**  

- NODE_SUCCEEDED (output?)

---

### 4.12 FailNode

**Guards**  

- node.status in { RUNNING, WAITING }（運用）
- cancelRequestedAt == null（デフォルト拒否）
- node.status が終端でない

**Emits**  

- NODE_FAIL_REPORTED（任意）
- NODE_FAILED

---

## 5. Orchestrator（プロセスマネージャ）責務

reducer / command-handler とは別に、以下を担当するコンポーネントを置く：

### 5.1 次ノード起動

- NODE_SUCCEEDED / NODE_FAILED / NODE_CANCELED を監視し、
  グラフ条件が満たされたら MarkNodeReady を内部発行する

### 5.2 Fork/Join 制御

- Fork到達 → FORK_OPENED（監査）＋ ブランチの NODE_READY 群を発行
- ブランチ終了 → JOIN_GATE_UPDATED を更新
- Join成立 → JOIN_PASSED（監査）＋ 次ノードの NODE_READY を発行

### 5.3 Cancel収束（重要）

- EXECUTION_CANCEL_REQUESTED を検知したら：
  - 実行中ノードに NODE_INTERRUPT_REQUESTED を発行（best-effort）
  - 未終端ノードの NODE_CANCELED を発行（収束）
  - 収束完了条件を満たしたら EXECUTION_CANCELED を発行（運用により即時でも可）

---

## 6. エラーハンドリング指針

- ガード違反は:
  - API: 409 Conflict / 422 Unprocessable Entity 相当
  - 内部: Rejected(CommandRejected) として返す（Eventは出さないのがデフォルト）
- 冪等コマンドは:
  - 同一効果なら 200/204 でOK（Event重複発行しない）

---

## 7. Cancel wins を破らないための必須チェック

- CancelExecution は最優先で受理される（終端以外）
- cancelRequestedAt が入ったら進行系コマンドを拒否する（デフォルト）
- reducer 側でも chooseExecStatus / normalize により Cancel を最終的に保証する
