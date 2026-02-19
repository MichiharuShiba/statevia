# Core State Machine Specification (Cancel Wins)

Version: 1.0
Project: 実行型ステートマシン

---

## 1. 目的

本仕様は、実行型ステートマシンの**コア機能**（状態遷移・イベント処理・競合解決）を定義する。  
UIは本仕様の結果を描画するだけであり、優先順位ロジックはコアが確定する。

---

## 2. 用語

- **Execution**: 1回の実行インスタンス。全ノードの状態を保持する集約（Aggregate）。
- **Node**: 実行単位（Task/Wait/Fork/Join/...）。
- **Event**: 状態変化を引き起こす入力（外部/内部）。
- **Command**: API/ユーザー操作等から来る要求。検証後、Eventに変換される。
- **Reducer**: Event を適用して状態を更新する純粋関数（副作用なし推奨）。
- **Effect/Action**: 実行エンジンが行う副作用（ジョブ開始、外部通知など）。

---

## 3. ステータスモデル

### 3.1 ExecutionStatus（集約の状態）

- ACTIVE（進行中）
- COMPLETED（正常終了）
- FAILED（異常終了）
- CANCELED（キャンセル終了）

**優先順位（確定ルール）**  
`CANCELED > FAILED > COMPLETED > ACTIVE`

> 同一タイムスライス/同一トランザクションで複数終端が競合した場合、常に CANCELED が勝つ。

### 3.2 NodeStatus（ノードの状態）

- IDLE（未到達）
- READY（実行可能）
- RUNNING（実行中）
- WAITING（待機中：外部入力待ち）
- SUCCEEDED（成功）
- FAILED（失敗）
- CANCELED（キャンセル）

**ノード優先順位（確定ルール）**  
`CANCELED > FAILED > SUCCEEDED > WAITING > RUNNING > READY > IDLE`

---

## 4. イベントモデル

### 4.1 イベント共通フィールド

- eventId: UUID
- executionId
- type
- occurredAt: timestamp
- actor: system | user | scheduler | external
- correlationId: 任意（API呼び出しなどと紐づけ）

### 4.2 代表イベント

#### Executionレベル

- EXECUTION_STARTED
- EXECUTION_CANCEL_REQUESTED
- EXECUTION_CANCELED
- EXECUTION_FAILED
- EXECUTION_COMPLETED

#### Nodeレベル

- NODE_READY
- NODE_STARTED
- NODE_WAITING
- NODE_RESUMED
- NODE_SUCCEEDED
- NODE_FAILED
- NODE_CANCELED

---

## 5. Cancel Wins のコアルール

### 5.1 Cancelは「最終確定」イベント

- Cancel要求（REQUESTED）と Cancel確定（CANCELED）を分ける
- ただし **Cancel要求が受理された時点で、以後の成功/失敗より優先**して扱う

### 5.2 競合解決（Conflict Resolution）

同一Execution内で以下が同時に起きうる:

- ノード成功とCancel
- ノード失敗とCancel
- Join成立とCancel
- ResumeとCancel

**解決規則**

1. まず `EXECUTION_CANCEL_REQUESTED` が存在すれば、後続の遷移は Cancel を優先
2. `EXECUTION_CANCELED` が適用されると、Executionは終端固定（不可逆）
3. ノードは可能な限り `NODE_CANCELED` に収束させる  
   - すでにSUCCEEDED/FAILEDになっているノードは「結果として残す」か「Canceledへ上書き」かを選べるが、コア仕様では下記を推奨

**推奨（監査・説明性重視）**

- ノードの既確定結果（SUCCEEDED/FAILED）は保持し、追加で `cancellationApplied=true` のようなメタを付与
- ただしUIや集約の最終状態は CANCELED として統一

---

## 6. 状態遷移規則

### 6.1 Execution遷移

- ACTIVE → CANCELED（Cancel wins）
- ACTIVE → FAILED
- ACTIVE → COMPLETED
- FAILED/COMPLETED/CANCELED は終端（不可逆）

### 6.2 Node遷移（基本）

- IDLE → READY → RUNNING → (SUCCEEDED | FAILED | WAITING)
- WAITING → (RUNNING via RESUME) → (SUCCEEDED | FAILED)
- 任意状態 → CANCELED（Cancel wins。ただし終端ノードは保持してもよい）

---

## 7. Fork/Join（並列制御）

### 7.1 Fork

- Fork到達で複数ブランチの先頭ノードを READY にする

### 7.2 Join

Joinは「合流条件」が満たされたときに次へ進める。

合流条件（デフォルト推奨）:

- 全ブランチが SUCCEEDED で Join成立
- いずれかが FAILED で Execution FAILED（ただしCancelがあればCancelが勝つ）
- Cancel要求が来たら Join成立判定よりCancelを優先

---

## 8. Wait/Resume/Cancel の相互作用（重要）

### 8.1 Wait

- NODE_WAITING になったノードは外部入力待ち

### 8.2 Resume

- Resumeは WAITING ノードに対してのみ有効
- ただし Cancel要求が受理済みなら Resumeは拒否されるか、受理しても結果は Cancelに収束

### 8.3 Cancel

- Cancel要求受理後は、全ての未終端ノードに対して Cancel収束を開始
- 実行中ノードには「中断要求（best-effort）」を発行する（エンジン側責務）

---

## 9. コマンド受理条件（ガード）

### 9.1 CancelExecution

- Executionが終端でなければ受理
- 受理したら即 `EXECUTION_CANCEL_REQUESTED` を発行

### 9.2 ResumeNode

- 対象ノードが WAITING であること
- Executionに Cancel要求が存在しないこと（存在するなら拒否推奨）

### 9.3 StartNode / CompleteNode

- Executionが終端でないこと
- Cancel要求が存在しないこと（存在するなら拒否またはNo-op）

---

## 10. イベント適用順序（同一バッチ内）

同一トランザクション/同一バッチで複数イベントを適用する場合の並び順:

1. Cancel関連（REQUESTED → CANCELED）
2. Failure関連
3. Success/Completion関連
4. Running/Waiting/Ready関連

これにより「Cancel wins」を機械的に担保する。

---

## 11. 監査性（Auditability）

- Cancel要求時刻と、Cancel確定時刻を分けて記録する
- 「Cancelが勝った」場合でも、もともと成功/失敗しそうだった事実はイベントで残せる
- UI/外部通知は、ExecutionStatusを一次情報として扱う

---

## 12. 実装ガイド（最小コア）

### 12.1 集約（Execution Aggregate）

保持すべき最低限:

- executionId
- status（ExecutionStatus）
- cancelRequestedAt（optional）
- nodes: Map<nodeId, NodeState>
- edges（またはgraph参照）
- version（楽観ロック用）

### 12.2 リデューサ（Reducer）

- apply(event, state) => newState
- 「優先順位」をif文で散らすのではなく、共通関数で固定化する（推奨）

---

## 13. 非目標

- UI描画ルール（別仕様）
- 通信方式（SSE/WebSocket等）
- 分散ロック/ワーカー実装詳細