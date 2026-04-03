# U8: 再起動ポリシーの具体

**前提**（modification-plan / open-decisions 4.3）: **再起動ポリシー**を用意し、ユーザー設定に応じて再起動時の挙動（**再実行**・**復元**・**失効**）を制御する。Phase 4 は当面スキップするが、event_store / projection / 再起動ポリシーは**実装する前提**で設計し、Phase 2 で考慮する（modification-plan 8.1, 4.2）。

本ドキュメントでは **再実行・復元・失効の意味とデフォルト**、**設定の持ち方**（DB・appsettings・環境変数）、および **Phase 2 で実装する最小範囲** を議論する。

---

## 1. 用語の整理

Core-API（C#）再起動時、**メモリ上の Engine は空**になる。DB の projection（workflows）には `status = Running` の行が残る可能性がある。

| 用語       | 意味（本計画での定義）                                                                                                                                                                                                                                                                                                                                                     |
| ---------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **失効**   | 再起動後、DB 上 `status=Running` の workflow は **status は変更せず**、**restart_lost（bool）を true に一括更新**する（C12 決定）。**Engine には再投入しない**。GET では projection を返すため status は Running のままだが **restart_lost: true** で「再起動で実行中ではなくなった」と判別する。Cancel/Event を送っても Engine にインスタンスがないため **409 または 410** で返す。 |
| **復元**   | 再起動後、`status=Running` の workflow について **event_store のイベント列をリプレイ**し、Engine に「状態のロード」相当で再投入する。Engine に **LoadState(workflowId, ExecutionState)** のような API が必要。復元後は通常どおり Cancel/Event が効く。                                                                                                                     |
| **再実行** | 再起動後、`status=Running` の workflow を **最初からやり直す**（Start 相当で再実行）。イベント列のリプレイではなく、**同じ definition で再度 Start** する。結果として同一 definition で複数回「開始」される可能性があり、workflow_id の扱い（新規 ID にするか、既存を上書きするか）の決定が必要。                                                                          |

**補足**: 「再実行」は「同じワークフロー ID で先頭から再適用」と解釈することもできる。その場合は event_store をクリアするか、新規 workflow_id で Start するかで解釈が分かれる。本ドキュメントでは **再実行 = 同じ definition で新規 Start（または既存 workflow を「最初の状態」にリセットして Engine に載せる）** とし、必要なら別案を 6.2 に残す。

---

## 2. 参照仕様・制約

### 2.1 Engine の責務（modification-plan 2.3, U7）

- Engine は永続化を持たず、**純粋なエンジンドメインのみ**。
- **復元**を実装する場合、Engine に「状態をロードする」API（例: `LoadState(workflowId, state)` または `Restore(workflowId, EventEnvelope[])`）を追加する必要がある。U7 で reducer は Engine に置くため、**event_store から取得した EventEnvelope[] を reducer で畳み込んだ結果**を Engine に渡すか、**Engine が EventEnvelope[] を受け取り内部で reducer を適用して状態を復元**するかのどちらかになる。

### 2.2 Phase 4 スキップ（modification-plan 8.1, C4）

- Phase 4（DB 安定化・再起動復元の拡張）は**当面スキップ**。
- **再起動時に status=Running の workflow をどう扱うか**（失効のままにするか、最小限の復元を Phase 2 で入れるか）が曖昧（C4）。本 U8 で「Phase 2 の最小範囲」を決める。

### 2.3 設定の候補

- **appsettings.json**（または appsettings.Development.json）: アプリ設定。デプロイ単位で変更可能。
- **環境変数**: コンテナ・Kubernetes 等で上書きしやすい。
- **DB**: テナントやシステム全体で「再起動ポリシー」を 1 件で持つ。将来のマルチテナントや運用画面から変更する場合に有利。v2 ではまず不要の可能性が高い。

---

## 3. 選択肢

### 3.1 デフォルトポリシー

| 案            | デフォルト                                                          | 理由                                                                                                                                                                |
| ------------- | ------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **A: 失効**   | 再起動時は **失効**（Running は Engine に再投入しない）             | 実装が最も軽い。Phase 2 で「考慮」する最小実装と相性が良い。運用では「再起動したら Running は諦める」と明示できる。                                                 |
| **B: 復元**   | 再起動時は **復元**（event_store からリプレイして Engine に載せる） | ユーザー体験は良いが、Engine に LoadState/Restore API を追加し、API の起動時処理（Running 一覧取得 → リプレイ → Engine に渡す）が必要。Phase 2 の範囲が大きくなる。 |
| **C: 再実行** | 再起動時は **再実行**（同じ definition で再 Start）                 | workflow_id の扱い（新規作成するか既存を再利用するか）と、event_store との整合が複雑。Phase 2 では見送り推奨。                                                      |

### 3.2 設定の持ち方

| 案                      | 設定の場所                                                               | メリット                           | デメリット                                                 |
| ----------------------- | ------------------------------------------------------------------------ | ---------------------------------- | ---------------------------------------------------------- |
| **A: appsettings のみ** | `RestartPolicy: "Invalidate" \| "Restore" \| "Replay"` 等                | 実装が簡単。環境ごとに変えやすい。 | DB や運用画面からは変えられない（v2 では不要の可能性大）。 |
| **B: 環境変数で上書き** | 例: `STATEVIA_RESTART_POLICY=Invalidate`。appsettings を環境変数で上書き | コンテナ・K8s で扱いやすい。       | キー名の規約を決める必要がある。                           |
| **C: DB に持つ**        | 例: `system_settings` テーブルに 1 行で保持                              | 将来、UI や API で変更可能。       | v2 では過剰。Phase 4 以降で検討でよい。                    |

**推奨の組み合わせ**: **デフォルトは失効（3.1 案 A）**。**設定は appsettings + 環境変数で上書き可能（3.2 の A+B）**。DB は Phase 4 以降（3.2 案 C は見送り）。

### 3.3 Phase 2 で実装する最小範囲

| レベル   | 内容                                                                                                                                                                                                                                                                           | Phase 2 での扱い                                                                                        |
| -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------- |
| **最小** | **失効のみ**実装。再起動時は **何もしない**（Running の workflow は Engine に再投入しない）。GET /workflows/{id} は projection を返すため「Running」のまま見える。Cancel/Events は「該当 workflow が Engine に存在しない」として **409 Conflict** または **410 Gone** で返す。 | 推奨: Phase 2 で実装する。                                                                              |
| **中**   | 失効に加え、**workflows の restart_lost フラグを true に更新**する。再起動直後に `status=Running` の行を一括で **restart_lost = true** に更新し、status はそのまま。API レスポンスで **restart_lost** を返すと「再起動で実行中ではなくなった」と判別できる（C12 決定）。 | Phase 2 で実装する。workflows に **restart_lost** (bool) 列を追加。db-schema.md に反映済み。             |
| **大**   | **復元**まで実装。Engine に LoadState/Restore、API 起動時に Running をリプレイして Engine に渡す。                                                                                                                                                                             | Phase 4 または Phase 2 の「拡張」として別タスク化推奨。                                                 |

**推奨**: Phase 2 の最小範囲は **「失効」＋ 再起動時に Running 行の restart_lost = true への一括更新**。復元・再実行は **設定で選択可能にしておくが、Phase 2 では「失効」以外は実装しない**（設定で Restore を選んでも、Phase 2 では無視して失効と同様に振る舞う、または「未実装」として 501 を返す）。

---

## 4. 失効時の API 挙動

- **GET /v1/workflows/{id}**: projection を返す。再起動後も **status は Running のまま**。**restart_lost: true** を返し、クライアントは「再起動で実行中ではなくなった」と判別する。
- **POST /v1/workflows/{id}/cancel**, **POST /v1/workflows/{id}/events**: Engine に該当 workflow が存在しない場合、**409 Conflict**（「ワークフローは再起動により実行中ではなくなりました」）または **410 Gone** を返す。レスポンス body に `code: "WorkflowNotRunning"` のようなエラーコードを入れるとクライアントが扱いやすい。

---

## 5. 設定キー案（appsettings / 環境変数）

- **appsettings.json**:  
  `"RestartPolicy": "Invalidate"`（または `"Restore"`, `"Replay"`。Phase 2 では `Invalidate` のみ実装）
- **環境変数**:  
  `STATEVIA__RESTART_POLICY=Invalidate`（ASP.NET Core の規約で `__` がネストしたキーに対応。大文字可）

---

## 6. 決定事項・オープンな論点（記入用）

### 6.1 決定事項

| 事項                 | 決定内容                                                                                                                                              |
| -------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| デフォルトポリシー   | **失効**（Running は Engine に再投入しない）（決定）                                                                                                  |
| 設定の持ち方         | **appsettings** で `RestartPolicy` を指定。**環境変数**（例: `STATEVIA__RESTART_POLICY`）で上書き可能。DB は Phase 4 以降で検討（決定）               |
| Phase 2 の最小実装   | **失効**を実装。再起動時は Running の workflow を Engine に再投入しない。Cancel/Events は該当が Engine にいなければ **409 または 410** で返す（決定） |
| 再起動時の projection 更新 | 再起動時に `status=Running` の行について **restart_lost = true** に一括更新する。**status は変更しない**（C12 決定。別フラグで管理）。                    |
| 復元・再実行         | Phase 2 では実装しない。設定に Restore/Replay があっても **501 Not Implemented** を返す（決定）                                                       |

### 6.2 オープンな論点

- **再実行**の厳密な意味（同じ workflow_id で先頭からリプレイするか、新規 workflow_id で Start するか）。Phase 5 以降で必要になったら定義する。
- **復元**を実装する場合の Engine API: `LoadState(workflowId, ExecutionState)` とするか、`Restore(workflowId, EventEnvelope[])` で Engine 内でリプレイするか。U7 の reducer が Engine にあるため、どちらも可能。Phase 4 で検討。
- ~~**Stale / Lost** を workflows.status の正式な値とするか、別フラグで持つか~~ → **決定**: **restart_lost** (bool) の別フラグで持つ（C12）。db-schema.md の workflows に **restart_lost** 列を追加済み。API レスポンスに **restart_lost** を含める。

以上を、U8 再起動ポリシーの具体の議論とする。
