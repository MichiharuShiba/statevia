# Input / Output 実装タスク分割

本ドキュメントは **`.workspace-docs/30_specs/20_done/v2-workflow-input-output-spec.md`（v0.3）** に基づき、実装を **依存関係のわかる単位**に分割したものです。
**Wait/Resume ペイロード**、**Fork=null ポリシー**、**高度なマッピング言語のみ**は **本リストのスコープ外**（別エピック）。

> **進捗（2026-03）**: **フェーズ A（IO-1〜IO-5）** は Core-Engine に実装済み（`IWorkflowEngine.Start(..., workflowInput)`、直列 `next`・Fork Broadcast・Join 辞書の伝播、`WorkflowInputPropagationTests`）。**IO-19**（YAML キー `input`）は Engine・仕様書に反映済み。

---

## 凡例

| 記号 | 意味 |
|------|------|
| **IO-*** | 本エピックのタスク ID |
| **完了条件** | マージ可能な最小成果物 |

---

## フェーズ A — エンジン（データの流れ）

| ID | タスク | 完了条件 | 依存 |
|----|--------|----------|------|
| **IO-1** | **（完了）`IWorkflowEngine.Start` に `workflowInput` を追加** | 初期状態の `ScheduleStateAsync` に `workflowInput` が渡る（既存呼び出しは `null` で互換）。 | - |
| **IO-2** | **（完了）直列 `next` で前 output → 次 input** | `ProcessFact` 経由の `transition.Next` で、直前状態の output を候補 raw として渡す。 | IO-1 |
| **IO-3** | **（完了）Fork: Broadcast** | Fork 遷移時、各分岐先の最初の状態に **同一 raw**（Fork 直前の output）を input として渡す。 | IO-2 |
| **IO-4** | **（完了）Join: 辞書そのまま（後勝ちルール）** | Join 完了後の次状態へ、`GetJoinInputs` を **そのまま** input として渡す（キー衝突時の後勝ちは仕様どおり）。 | IO-2 |
| **IO-5** | **（完了）単体テスト** | 上記（IO-1〜4）の回帰テスト（最小グラフで可）。 | IO-1〜IO-4 |

---

## フェーズ B — 定義（YAML）と `input`

| ID | タスク | 完了条件 | 依存 |
|----|--------|----------|------|
| **IO-6** | **（完了）状態定義に `input` を持つモデル** | `StateDefinition`（またはコンパイル済みメタ）に `input` フィールドを追加し、ローダが読み取れる。 | - |
| **IO-7** | **（完了）マッピング評価の最小実装** | **1 種類から**でよい（例: 恒等、`null`、または **JSONPath/JMESPath のサブセット**）。定義に無い場合は raw 通過。 | IO-6 |
| **IO-8** | **（完了）`WorkflowEngine` で遷移先へ渡す直前に適用** | `ScheduleStateAsync` 前に、遷移先状態の `input` があれば **raw に適用**（§3.2、§3.3、§3.4）。 | IO-2〜IO-4, IO-7 |
| **IO-9** | **（完了）コンパイル時検証（可能な範囲）** | 未知のマッピング演算子・構文エラーは **コンパイル失敗**または **明確なエラーメッセージ**（仕様 §6.1）。 | IO-6, IO-7 |
| **IO-10** | **（完了）定義ドキュメント同期** | `docs/core-engine-definition-spec.md` または `.workspace-docs/30_specs/20_done/v2-workflow-definition-spec.md` と **実装の記法**を一致。 | IO-6〜IO-9 |

### フェーズ B の残タスク（拡張）

| ID | タスク | 完了条件 | 依存 |
|----|--------|----------|------|
| **IO-16** | **（完了）複数マッピング対応** | `input` で複数ルールを定義でき、1 回の評価で複数キーを構築できる。 | IO-7〜IO-9 |
| **IO-17** | **（完了）独自キー値の割当** | `foo: $.a` のように、任意キーへ抽出結果を代入できる。 | IO-16 |
| **IO-18** | **（完了）ネストキー割当** | `foo.bar: $.a.b` のようなドットキーでネストオブジェクトを生成できる（競合時ルール明記）。 | IO-17 |
| **IO-19** | **（完了）名称統一（`inputMapping` → `input`）** | 実装・テスト・仕様書の表記を `input` に統一する。 | IO-6〜IO-10（**実装済み 2026-03**） |
| **IO-20** | **（完了）リテラル値入力（文字列/数値/真偽値/null）対応** | `input` で JSONPath だけでなくリテラル値を直接指定できる（例: `foo: "my song"`, `bar: 2.0`, `flag: true`）。評価規則と型保持をテストで保証。 | IO-16〜IO-19 |

> 将来積み残し（`$.context` / `$.env` など）は **`input-future-backlog.md`** で管理する。

### IO-20 仕様案（確定候補）

`input` の各値は **「式」または「リテラル」** として扱う。

#### 1) 判定ルール（最小で曖昧さを避ける）

- 文字列で **`$.`** または **`$`** から始まる値は **パス式**として評価する。
  - 例: `$.a.b`, `$`
- それ以外は **リテラル値**としてそのまま使う。
  - 例: `"my song"`, `2.0`, `true`, `false`, `null`, `{ x: 1 }`, `[1, 2]`

#### 2) 構文（`input` 名称に統一後）

```yaml
states:
  B:
    input:
      foo: $.a
      foo.bar: $.a.b
      title: "my song"
      retry: 2
      enabled: true
      note: null
```

#### 3) 評価結果の構築規則

- `input` がマップの場合、キーごとに評価して 1 つのオブジェクトを組み立てる。
- キーにドット（例: `foo.bar`）が含まれる場合はネストオブジェクトとして構築する。
- パス式が未解決の場合は `null` を代入する（実行失敗にはしない）。
- 競合時（例: `foo` と `foo.bar` の同時指定）は **後勝ち**（YAML の記述順）とする。

#### 4) 型の扱い

- リテラル値は YAML デシリアライズ結果の型を保持する。
  - 文字列: `string`
  - 整数: `long`（現行ローダー仕様）
  - 小数: `double`
  - 真偽値: `bool`
  - `null`: `null`
- オブジェクト / 配列リテラルは辞書 / リストとして保持する。

#### 5) バリデーション方針

- `input` がスカラー（例: `input: $.a`）の場合は **単一式ショートハンド**として許可する（将来拡張で無効化可）。
- `input` がマップの場合、値の型は以下を許可:
  - パス式文字列（`$` / `$.`）
  - 文字列リテラル
  - 数値
  - 真偽値
  - `null`
  - オブジェクト
  - 配列
- 未知キー禁止は「式の種別キー方式」を採用する場合に適用し、本案（暗黙判定方式）では適用しない。

---

## フェーズ C — Core-API

| ID | タスク | 完了条件 | 依存 |
|----|--------|----------|------|
| **IO-11** | **（完了）`POST /v1/workflows` の `input` フィールド** | `StartWorkflowRequest`（または契約 DTO）に JSON 任意オブジェクト。`WorkflowService` → `Start(..., input)`。 | IO-1 |
| **IO-12** | **（完了）冪等・dedup と input** | `command_dedup` のリクエストハッシュに **`input` を含む**（仕様 §4.2）。同一 definitionId でも input 違いは別リクエスト。 | IO-11 |
| **IO-13** | **（完了）契約ドキュメント** | `docs/core-api-interface.md` に `input` を追記。 | IO-11 |

---

## フェーズ D — 運用・品質（任意）

| ID | タスク | 完了条件 | 依存 |
|----|--------|----------|------|
| **IO-14** | **（完了）Read Model / ログ方針** | workflow input・state output を **どこまで返すか**（サイズ・マスキング）を AGENTS または docs に 1 行以上。 | IO-11 |
| **IO-15** | **（完了）E2E スモーク** | API で `input` 付き起動 → グラフ or レスポンスで **入力が伝播したこと**を確認できる。 | IO-8, IO-11 |

---

## 推奨実施順（マイルストーン）

1. **M1: エンジンだけ** — IO-1 → IO-2 → IO-3 → IO-4 → IO-5  
2. **M2: マッピング** — IO-6 → IO-7 → IO-8 → IO-9 → IO-10 → IO-16 → IO-17 → IO-18 → IO-19 → IO-20  
3. **M3: API** — IO-11 → IO-12 → IO-13  
4. **M4: 仕上げ** — IO-14, IO-15（任意）

---

## ブランチ名（推奨）

エピック共通の接頭辞は **`feature/io-`**（Input/Output）。**main へマージする単位＝フェーズ**を推奨（レビュー・リリースノートが追いやすい）。

| フェーズ | 推奨ブランチ名 | 含むタスク | 備考 |
|----------|----------------|------------|------|
| **A** エンジン | `feature/io-a-engine-workflow-input` | IO-1〜IO-5 | `Start`・next・Fork Broadcast・Join 辞書・テスト |
| **B** 定義・マッピング | `feature/io-b-yaml-input` | IO-6〜IO-10, IO-16〜IO-20 | モデル・評価・エンジン適用・検証・ドキュメント＋複数/独自/ネストマッピング＋名称統一＋リテラル入力 |
| **C** Core-API | `feature/io-c-api-workflow-start-input` | IO-11〜IO-13 | `POST` の `input`・dedup・`core-api-interface` |
| **D** 任意 | `feature/io-d-readmodel-e2e-optional` | IO-14, IO-15 | 任意フェーズのため **ブランチを分けない**で C に続けても可 |

**短い別名**（好みで置き換え可）:

| フェーズ | 短名 |
|----------|------|
| A | `feature/io-a-engine` |
| B | `feature/io-b-input` |
| C | `feature/io-c-api` |
| D | `feature/io-d-polish` |

**依存順**: 原則 **`feature/io-a-*` → `feature/io-b-*` → `feature/io-c-*`**（A を main に先マージ）。D は **C の後**、または C と同一ブランチでよい。

---

## 参照

- `.workspace-docs/30_specs/20_done/v2-workflow-input-output-spec.md`
- `.workspace-docs/30_specs/20_done/v2-workflow-definition-spec.md` §5.1
- `.workspace-docs/50_tasks/10_in-progress/v2-input-future-backlog.md`（将来積み残し）

---

## 変更履歴

| 版 | 日付 | 内容 |
|----|------|------|
| 0.1 | 2026-03 | 初版（タスク分割） |
| 0.2 | 2026-03 | フェーズ別ブランチ名（推奨）を追加 |
| 0.3 | 2026-03 | フェーズ A 実装完了を追記 |
| 0.4 | 2026-03 | フェーズ B 残タスク（IO-16〜IO-18: 複数/独自/ネストマッピング）を追記 |
| 0.5 | 2026-03 | フェーズ B に `inputMapping`→`input` の名称統一タスク（IO-19）を追加 |
| 0.7 | 2026-03 | IO-19 実装（Engine: `StateDefinition.Input` / `StateInputs`） |
| 0.8 | 2026-03 | IO-16/17/18/20 実装（複数/独自/ネスト/リテラル input を Engine とテストに反映） |
| 0.9 | 2026-03 | 将来積み残し（`$.context` / `$.env`）を `input-future-backlog.md` へ分離 |
| 0.6 | 2026-03 | フェーズ B にリテラル値入力対応タスク（IO-20）を追加 |
| 1.0 | 2026-03 | フェーズ C 実装（IO-11/12/13: API input・dedup requestHash・契約ドキュメント更新） |
| 1.1 | 2026-03 | フェーズ D 反映（IO-14: AGENTS に露出方針を追記、IO-15: スモーク手順追加と実測実行を完了） |
