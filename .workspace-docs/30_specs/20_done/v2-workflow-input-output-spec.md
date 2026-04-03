# ワークフロー Input / Output（API・エンジン連携）仕様

Version: 0.4  
Project: Statevia v2  
Status: **設計案**（Fork / Join / states の `input` / Wait・Resume のスコープは v0.3 で更新）

---

## 0. 目的

- ワークフロー開始時に **1 つの JSON ペイロード（workflow input）** を渡す。
- **Action 相当の状態**が返す **output** を、既定では **直列 `next` の次状態の input** として引き渡す（**YAML 上 `states.<name>.input` がある場合はその適用後**を渡す）。
- Core-API から Core-Engine へ渡すための **契約（HTTP・型・ポリシー）** を v2 計画配下に固定する。

関連ドキュメント:

- `docs/core-engine-definition-spec.md`（states / nodes）
- `.workspace-docs/30_specs/20_done/v2-workflow-definition-spec.md`（states 形式・`input` の記述枠）
- `docs/core-engine-commands-spec.md`（CreateExecution / StartExecution の `input?`）
- `docs/core-api-interface.md`（HTTP 契約）

---

## 1. 現状整理（実装ベースライン）

| レイヤ | 内容 |
|--------|------|
| **`IState` / `IStateExecutor`** | `ExecuteAsync(ctx, input, ct)` で **input / output は `object?` として既に存在**。 |
| **`WorkflowEngine`** | 状態完了時に `SetOutput(stateName, output)`。**Join** は `JoinTracker.GetJoinInputs(joinState)` で **分岐状態名 → output** を集約済み。 |
| **直列 `next`** | 現状、`ProcessFact` から次状態を起動する際 **input は常に `null`**（前状態 output 未配線）。 |
| **初期状態** | `Start` 直後の最初の状態にも **workflow 入力は未配線**（`null`）。 |
| **Core-API** | 定義コンパイルは主に **NoOp / Wait** で、意味のある Action と入出力は未配線。 |

本仕様は、上記ギャップを **意図的な振る舞い** で埋めるためのものである。

---

## 2. 用語

| 用語 | 意味 |
|------|------|
| **Workflow input** | クライアントが **ワークフロー開始時**に渡す 1 つの JSON 互換オブジェクト。 |
| **State output** | ある状態の `IStateExecutor.ExecuteAsync` の戻り値（`object?`）。実行グラフのノード完了事実と紐づく。 |
| **Action 相当の状態** | 定義上「通常の実行」を行う状態。現実装では Wait 以外の状態に相当（名称は実装・YAML と整合）。 |

---

## 3. エンジン側ポリシー

### 3.0 output と input の基本原則

- 各状態の **output** は、ユーザー定義状態 `IState<TInput, TOutput>.ExecuteAsync(...)` の戻り値。
- 次状態の **input** は、遷移時に以下で決まる:
  - 遷移先状態に `input` 定義が **ない** → 直前 output を **そのまま**渡す（raw 通過）
  - 遷移先状態に `input` 定義が **ある** → `input` 定義から構築した値のみを渡す
- `input` 定義がある場合、**マッピングしていない output フィールドは自動で引き継がれない**。

### 3.1 ワークフロー開始

- `IWorkflowEngine.Start`（または同等のエントリ）に **`workflowInput: object?`** を追加する。
- **初期状態**の `ScheduleStateAsync(..., initialState, ..., input)` の **`input` に `workflowInput` を渡す**。

### 3.2 直列遷移（単一 `next`）と **states の input**

1. **候補 input（raw）** を求める: **`raw = output_previous`**（直前に完了した状態の output）。`Completed` 事実で `transition.Next` に進む場合が対象。
2. **遷移先の状態**（次の **Action 相当**を含む実行状態）の定義に **`input` が存在する場合**、エンジンは **`raw` に対してマッピングを適用**し、得られた値を **`input_next`** とする。
3. **`input` が無い、または無効な場合**は **`input_next = raw`**（恒等）。

**マッピングの適用タイミング**: 次状態の `ScheduleStateAsync` に渡す **直前**（executor の `ExecuteAsync` に入る直前）。

**マッピング式の言語**（JSONPath / JMESPath / テンプレート等）は **実装で確定**する。本仕様では **「存在する場合は raw を変換してから次へ渡す」** ことのみを規定する。YAML スキーマの置き場所は **`.workspace-docs/30_specs/20_done/v2-workflow-definition-spec.md`** §「input」に従う。

**補足（nodes 形式）**: `.workspace-docs/30_specs/20_done/v2-definition-spec.md` の action `input` も **states と同じ `$` / `$.` のみ**とし、**`${...}` 形式のテンプレは採用しない**（現時点で導入予定なし。`.workspace-docs/30_specs/10_in-progress/v2-nodes-to-states-conversion-spec.md` §6）。

**Fork / Join の直後**も同様に、**各分岐先・Join 後の次状態**に `input` があれば、**その直前の候補 input**（Broadcast 値、または Join 辞書）に対して適用してから次の Action に渡す。

### 3.3 Fork（並列分岐）— **確定: Broadcast**

- Fork 直前に完了した状態の **`output` を参照**し、その **同一参照（または実装上はシャローコピー方針を別紙で明示）** を、各分岐の **最初の状態**の `input` に渡す。
- **参照型**の `output` を複数ブランチに渡す場合、**ミュータブルなオブジェクトを共有するとレース**になり得る。実装では **ディープコピーしない**前提とし、クライアント／State 実装側で **イミュータブルまたはブランチ専用クローン**とするか、ドキュメントで注意喚起する。

※ 将来、YAML で `forkInputPolicy` を追加して **null 分岐**を選べるようにしてもよい（本書 v0.2 の既定は Broadcast のみ）。

### 3.4 Join（合流）— **確定: 辞書そのまま・キー衝突時は後勝ち**

- Join 完了後に遷移する **次の 1 状態**の `input` は、**ラッパーなし**の **`IReadOnlyDictionary<string, object?>`** とする。
- **キー**: 分岐側の **状態名**（`allOf` に含まれる各状態名）。**大文字小文字の扱いは既存の states / JoinTracker と同一**（現状は OrdinalIgnoreCase 相当）。
- **値**: 当該状態が `Completed` したときの **output**。
- **後勝ち**: 辞書を構築する過程で **同一キーへの代入が複数回発生する場合**（例: 実装のリファクタ・拡張でマージ経路が増える）、**最後に書き込んだ値が有効**とする。現行 `JoinTracker` は分岐状態名ごとに 1 エントリのため通常は衝突しないが、**将来互換**として明記する。
- HTTP / JSON で表現する場合は **オブジェクト 1 つ**（キー = 状態名、値 = シリアライズ済み output）。**キーが JSON 上で衝突する**（正規化後に同一キーになる）場合も **後勝ち**とする（実装側で正規化順序を固定すること）。

### 3.5 Wait / Resume — **本バージョンではスコープ外**

- **Wait 状態**の executor 契約（イベント名・完了事実）は **従来どおり**。**Wait 完了後にクライアントから任意 JSON を渡すペイロード**（Resume body など）は **本バージョンでは実施しない**（API・エンジンとも未実装）。
- 上記ペイロードは **将来バージョン**で `docs/core-api-interface.md` / `docs/core-engine-commands-spec.md` と **別途**定義する。

### 3.6 ユーザー定義状態での実装指針

- 状態実装は `IState<TInput, TOutput>` を実装し、`ExecuteAsync` の戻り値を output として返す。
- `TInput` は YAML の `input` 定義と整合する型を期待する。
  - 例: `input.path` の単一抽出なら `string` / `long` など単一型
  - 例: `input` マップ形式なら `IReadOnlyDictionary<string, object?>` など辞書型
- 型不一致はコンパイル時ではなく実行時に問題化しやすいため、状態実装側で null/型チェックを行う。

---

## 4. Core-API 契約（HTTP）

### 4.1 ワークフロー開始

**POST /v1/workflows** のリクエストに、任意フィールドとして追加する:

| フィールド | 型 | 必須 | 説明 |
|------------|-----|------|------|
| `input` | JSON オブジェクト（任意の構造） | 否 | **Workflow input**。省略時は `null`（エンジンでは初期状態の input が `null`）。 |

既存フィールド（`definitionId` 等）との関係は `docs/core-api-interface.md` に従う。

### 4.2 冪等・重複排除

- `command_dedup` に **リクエスト本文のハッシュ**を含める場合、**`input` を含めた正規化 JSON** でハッシュ対象とする（同一 `definitionId` でも input が異なれば別リクエストとして扱う）。

### 4.3 Read Model（任意）

- 監査・UI 用に、レスポンスまたは snapshot に **workflow input の要約**や **直近 state output** を載せるかは **別途プライバシー・サイズ上限**とともに決定する。

---

## 5. 定義（YAML）との関係

- **必須ではない**: 任意の状態に **`input`** を付けない場合、候補 input は **そのまま**次状態へ渡る（§3.2、§3.3、§3.4）。
- **任意の拡張**: **`input` が付いている場合**に限り、**raw の変換**を行ってから次の **Action**（または該当状態）へ渡す（§3.2）。**記述形式**は `.workspace-docs/30_specs/20_done/v2-workflow-definition-spec.md` に合わせる。
- Fork 専用の **`forkInputPolicy`**（broadcast 以外）は **将来**の拡張。v0.3 の既定は **Broadcast**（§3.3）。

---

## 6. 非機能・制約

- **ペイロードサイズ上限**: API ゲートウェイ・DB に合わせて上限を設ける（例: 数 100 KB 以下を推奨）。
- **ログ**: workflow input / state output に **機微情報**が含まれないようマスキング方針を運用ドキュメントに記載する。

---

## 6.1 設計上の懸念・未決定（チェックリスト）

Fork / Join の形を決めたうえで、実装・運用で触れておくとよい点:

| 項目 | 内容 |
|------|------|
| **Wait / Resume ペイロード** | **本バージョンでは対象外**（§3.5）。将来バージョンで別途。 |
| **`null` output の連鎖** | 直列 `next` で **前状態の output が `null`** のとき、次も `null` が入る。**意図した挙動か**を State 実装で統一する。 |
| **`Unit` / NoOp の output** | 現状の NoOp は `Unit` を返す。**次状態の input は `Unit` オブジェクトとして渡る**想定になる。JSON 連携時は **API 層で `null` に正規化するか**方針を決める。 |
| **Broadcast とミュータビリティ** | 同一 `output` 参照を複数タスクが並列で触ると **データ競合**。共有しない・読み取り専用にする・コピーするのいずれかを運用で要求。 |
| **Join 辞書のキー順** | JSON オブジェクトの **キー順は仕様上は保証しない**（クライアントは順序依存しない）。デバッグ用ログで順序を固定したい場合は **状態名ソート**を推奨。 |
| **event_store / snapshot** | input・各 output を **どこまで永続化するか**（サイズ・PII・リプレイ必要性）。冪等キャッシュに **input を含める**ことは §4.2 のとおり。 |
| **型の一貫性** | エンジンは `object?` のまま。**ある状態が `JsonElement`、次が `Dictionary` を期待**するなどは実行時まで検知されにくい。スキーマ検証は後続フェーズ。 |
| **states の `input` 失敗時** | マッピング式の評価エラー・参照パス欠落は **422 / 実行時エラー**のいずれかを実装で決める（コンパイル時検証可能なら望ましい）。 |

---

## 7. 実装フェーズ（推奨順）

1. **エンジン**: `Start(..., workflowInput)`、**`next` / Fork Broadcast / Join 辞書**から **候補 raw** を算出し、**遷移先に `input` があれば適用**してから `ScheduleStateAsync`。
2. **定義 YAML**: `input` の記法（§5、`.workspace-docs/30_specs/20_done/v2-workflow-definition-spec.md`）と **コンパイル時検証**の範囲。
3. **API**: `StartWorkflowRequest` に `input`（JSON）、`WorkflowService` → `IWorkflowEngine` へ伝搬。
4. **ドキュメント**: `docs/core-api-interface.md` / `docs/core-engine-commands-spec.md` への反映。
5. **Wait/Resume ペイロード**、**Fork ポリシー `null`**、**高度なマッピング言語**は **本バージョン以降**。

---

## 8. 変更履歴

| 版 | 日付 | 内容 |
|----|------|------|
| 0.1 | 2026-03 | 初版（v2 配下に設計として追加） |
| 0.2 | 2026-03 | Fork=Broadcast、Join=辞書そのまま＋キー衝突時後勝ちを確定。懸念チェックリスト（§6.1）を追加。 |
| 0.3 | 2026-03 | Wait/Resume ペイロードは本バージョン対象外と明記。YAML `input` による raw→次 input 変換を必須仕様に追加。 |
| 0.4 | 2026-03 | states レベルのキー名を `inputMapping` から `input` に整理。 |
