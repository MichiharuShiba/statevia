# CLAUDE.md

このファイルは `.cursor/rules/` の内容を Claude Code 向けに変換したものです。

---

## シニアエンジニアとしての検討・提案

ユーザーがプロンプトで方針・仕様・実装案・前提を述べたとき、**シニアエンジニアの立場で**次を検討する。

### いつ言うか

- 設計・API・データモデル・運用・セキュリティ・後方互換・パフォーマンスに**実害や不可逆なコスト**がありそうなとき。
- ユーザーの前提が**仕様・コードベースと食い違う**可能性があるとき。
- **より安全・単純・保守しやすい代替**が明らかなとき。

**言わなくてよい例:** 既に合意済みの細部、自明なリネーム、ユーザーが明示的に「これだけ」と閉じている極小タスク（その場合は短く確認するに留めてよい）。

### どう言うか

- **懸念・反論・トレードオフ**を、根拠つきで**簡潔に**述べる（長い説教や全面否定は避ける）。
- 可能なら **代替案** または **条件付きで進めるならの注意点** を 1 つ以上示す。
- ユーザーが決めれば進める話題では、**判断はユーザーに委ねる**旨をはっきり書く。
- 不確実なときは「確認したい点」として列挙し、断定しすぎない。

目的は作業を止めることではなく、**見落としやすいリスクを共有してから一緒に進める**こととする。

---

## コミットメッセージ規約

コミットメッセージは必ず**日本語**で記述する。英語や他言語を避け、文脈に応じて簡潔かつ内容が分かるように記載する。

例:

- バグ修正: 「〇〇のバグを修正」
- 機能追加: 「〇〇機能を追加」
- リファクタ: 「〇〇をリファクタ」

マージコミットや初回セットアップ時も日本語を使うこと。

---

## C# コーディング規約

### 変数名

- **略しすぎない**: 変数名は意味が分かるように十分な長さで書く。短い略称（例: `evt`, `msg`, `tmp`）より、意図の分かる名前（例: `envelope`, `message`, `buffer`）を優先する。
- 予約語と衝突する場合は別名を使う（例: C# の `event` は予約語のため、イベント用には `envelope` や `eventEnvelope` など）。

### コメント

- **public / internal の型・メソッド・プロパティ**: XML ドキュメントコメント（`/// <summary>` 等）を必ず付ける。
- **private のメソッド・フィールド・プロパティ**: 役割が自明でないものには `/// <summary>` を付ける。ヘルパーやマッピング、分岐の意図が分かるようにする。
- 複雑な分岐・ビジネスルールには、必要に応じて仕様やセクション参照（例: core-reducer-spec §4.2）をコメントで記載する。

### パターンマッチング

- **可能な限りパターンマッチングを使う。**
  - 型チェック＋キャスト: `x is string s` の形で変数に取り、続けて `s` を使う。
  - null 判定: `x is null` / `x is not null` を優先する。
  - 定数・enum: `value is EnumType.Member`、複数候補は `value is A or B or C`。
  - 型による分岐: `switch (obj) { case int i: ... case string s: ... }` や switch 式 `obj switch { int i => ..., _ => ... }` を検討する。
- 辞書の取得は `TryGetValue` と組み合わせつつ、その後の型判定は `is` パターンや switch 式で書く。
- 従来の `as` + null チェックより、`is` パターンや switch 式を優先する。

### 条件分岐（if 最小化）

- **if/else if の多段連鎖は最小限にする。** 分岐の基本は `switch` / switch 式 / パターンマッチを優先する。
- **網羅性を担保する。** enum や判別可能な値の分岐では、`switch` で全ケースを明示し、未対応ケースを検知できる構成を優先する。
- **暗黙フォールバックを避ける。** 末尾の `else` で黙って吸収せず、未対応は明示的に失敗させる（例外・エラー返却）か、意図をコメントで明記する。
- **if を使ってよい場面を限定する。**
  - 早期 return のガード節（null/前提条件/権限判定）
  - 2 分岐で最も読みやすい場合
  - 計測で性能上の妥当性が確認できる場合（理由をコメントで残す）

### 改行・整形の一貫性

- **同一メソッド内での改行方針を統一する。**
- とくに `if` が連続し、同様の処理（例: バリデーション後のエラー生成と `return` / `throw`）が続く場合は、オブジェクト初期化子や改行スタイルを揃える。
- 「1行で収まるから単行」「一部だけ複数行」の混在を避け、レビュー時の差分ノイズを減らす。

### 単体テスト

- **テストの目的**: 各テストメソッドが「何を検証するか」を必ずコメントで書く。
  - `/// <summary>…</summary>` で「〇〇であること」「〇〇のとき △△になること」のように日本語で記載する。
- **AAA（Arrange-Act-Assert）**: テスト本体は次の3ブロックに分け、それぞれのブロックの直前にコメントで区切る。
  - `// Arrange` … 前提条件（状態・イベント・入力）の準備。
  - `// Act` … 被テスト対象の実行（メソッド呼び出しなど）。
  - `// Assert` … 期待結果の検証（Assert のまとまり）。
- 1テスト1シナリオにし、Arrange / Act / Assert の流れが読み取りやすいようにする。

---

## TypeScript / React コーディング規約

対象は主に **`services/ui/`**（Next.js）。インポートの並び・ファイル分割などは **既存コードに合わせる**。

### 変数名

- **略しすぎない**: 変数名は意味が分かるように十分な長さで書く。短い略称より、意図の分かる名前を優先する。
- 予約語や慣用と衝突しやすい名前（例: `event`）は、用途に応じて `mouseEvent` / `payload` など別名を使う。

### コメント

- **`export` する関数・型・定数・React コンポーネント**: 公開 API として再利用されるものには **JSDoc**（`/** ... */`）で要約を付ける。引数・戻り値・副作用・呼び出し側の前提が分かりにくいときは `@param` / `@returns` も使う。
- **モジュール内の非 export（ヘルパー・複雑な分岐）**: 役割が自明でないものには `/** ... */` または `//` で意図を書く。
- 複雑な分岐・ドメインルールには、必要に応じて仕様やドキュメントへの参照をコメントで残す。

### 型の絞り込み

- **可能な限り絞り込み（ナローイング）で表現する。** `as` で押し切るのは最後の手段にする。
  - 判別可能なユニオン: `kind` や `type` などのタグで `switch` し、各分岐で型が絞れるようにする。
  - `typeof` / `instanceof` / `in` オペレータ・ユーザー定義型ガード（`function isFoo(x): x is Foo`）を活用する。
  - 外部入力・`JSON.parse` 直後などは **`unknown` を受けてから** 検証・絞り込みし、`any` を増やさない。
- オブジェクトリテラルで型を固定したいときは、チームで既に使っている場合に限り **`satisfies`** の利用を検討する（無闇に多用しない）。

### 条件分岐（if 最小化）

- **if の多段連鎖は最小限にする。** 分岐の基本は `Record`（マップ参照）または `switch` を優先する。
- **網羅性を担保する。** 判別可能なユニオンや literal union の分岐では、`Record<Union, ...>` または `switch + never` で未対応ケースをコンパイル時に検知できる形を優先する。
- **暗黙フォールバックを避ける。** 最後の `return` / `else` で黙って吸収する書き方は避け、未対応を型や明示エラーで検出できるようにする。
- **if を使ってよい場面を限定する。**
  - 早期 return のガード節（null/入力前提/権限判定）
  - 2 分岐で最も読みやすい場合
  - 計測で性能上の妥当性が確認できる場合（理由をコメントで残す）

### 改行・整形の一貫性

- **同一関数内での改行方針を統一する。**
- とくに `if` が連続し、同様の処理（例: バリデーション後の `setToast` と `return`）が続く場合は、オブジェクトリテラルや改行スタイルを揃える。
- 「1行で収まるから単行」「一部だけ複数行」の混在を避け、レビュー時に差分ノイズを増やさない。

### React（UI）

- コンポーネントの **props には明示的な型**（`type Props = { ... }` または `interface`）を付ける。`children` を取る場合も含め、公開意図に合わせて型を公開する。
- **表示専用の小さなコンポーネント**でも、契約が複雑なら JSDoc で「何を表示し、何を受け付けないか」を短く書く。
- **UI文言は i18n 辞書に集約する。** 画面実装（`*.tsx`）にハードコード文言を直接書かず、`uiText` / `uiText.en` 等の辞書キー経由で参照する。
- バリデーションメッセージ、ラベル、説明文、エラーメッセージも例外なく i18n に準拠する。

### 単体テスト（Vitest）

- **テストの目的**: 各ケースが「何を検証するか」を必ず分かるように書く。
  - `it` / `test` の **説明文字列は日本語**でよい（例: 「成功時はパースした JSON を返す」）。複合的なケースは、**ケース直前の JSDoc**（`/** ... */`）で目的を補足してもよい。
- **AAA（Arrange-Act-Assert）**: テスト本体は次の3ブロックに分け、それぞれのブロックの直前にコメントで区切る。
  - `// Arrange` … モック・入力・レンダー用のラッパーなどの準備。
  - `// Act` … 被テスト対象の実行。
  - `// Assert` … `expect` などによる検証のまとまり。
- **1 ケース 1 シナリオ**にし、Arrange / Act / Assert の流れが読み取りやすいようにする。

### 品質チェック

- 変更後は **`tsc --noEmit`** と、該当範囲の **`npm run test:run`** が通る状態にする（リポジトリ方針。ESLint は未設定）。

---

## Markdown ドキュメントガイドライン

*対象: `**/*.md` ファイルの生成・編集時*

1. **言語設定:** AIがMarkdownドキュメント（主に `.spec-workflow/specs/` などの仕様書）を生成・編集する際、本文は「日本語」で記述する。（テンプレートに含まれる英語の見出しなどはそのまま維持して構いません）
2. **フォーマット:** プロジェクト直下の `.markdownlint.json` に準拠したフォーマットにする。特に以下の点に注意する:
   - 見出しの前後には適切な空行を入れる。
   - リストまわりやコードブロックの前後にも空行を入れる。
   - 行末に不要なスペース（Trailing spaces）を残さない。
3. **検証:** Markdownファイルを編集した後には、以下のコマンドで静的チェックを行い、エラーがないことを確認してから完了報告をする。
   `npx markdownlint-cli2 "<変更したファイルパス>"`
4. **Mermaidラベルの余白統一:** `flowchart` のエッジラベルで視認性を上げる必要がある場合は、左右に `&nbsp;&nbsp;` を付与して表記を統一する。例: `-->|&nbsp;&nbsp;一致あり&nbsp;&nbsp;|`。同一図内で適用する場合は、同種ラベル（`OK/NG/Yes/No` など）に一貫して適用する。
5. **Spec Workflowテンプレート準拠:** `.spec-workflow/specs/` および `.spec-workflow/steering/` 配下のMarkdownを新規作成・更新する場合は、必ず `.spec-workflow/templates/` 配下の対応テンプレートに沿って記述する。具体的には `requirements-template.md` / `design-template.md` / `tasks-template.md` / `product-template.md` / `tech-template.md` / `structure-template.md` の見出し構成・必須項目を維持する。

---

## `.workspace-docs` 運用ルール

*対象: `.workspace-docs/**/*.md` ファイルの操作時*

正本・経緯・ひな形: `.workspace-docs/20_discussion/docs-format-unification.md`

### トップレベルフォルダ

- `00_inbox/`: 一時置き（運用見て再検討）
- `10_notes/`: 開発者の思想・メモ・調査
- `20_discussion/`: 開発者と AI の合意前の議論
- `30_specs/`: 仕様
- `40_plans/`: 計画・方針
- `50_tasks/`: タスク・**backlog のみここ**（`backlog` 種別も `50_tasks` のみ）

### 状態サブフォルダ（各カテゴリ共通）

`10_in-progress/` → `20_done/` → `30_archived/`（A-Z 並び用の番号付き）。文書のライフサイクルは **ここで表す**。メタブロックに **Status は書かない**。

### 新規ファイル名（既存は触らない）

- 形式: `{何を}_{どうする}_{種別}.md`
- セグメント間: `_`、セグメント内: `-`（小文字 kebab-case）
- ファイル名にバージョン接頭辞（例: `v2-`）は付けない
- 状態フォルダ名をファイル名に含めない

**種別（この 5 語のみ）:** `spec` | `plan` | `tasks` | `backlog` | `discussion`

### 新規 Markdown の先頭メタブロック

タイトル直後に箇条書き → 空行 → `---` → 本文。

必須:

- `Version:` セマンティックバージョン（`1.2.3` のみ、`v` なし）。未リリース期の新規は **`1.0.0` から開始**。リリース後は文書変更時に必ず増やす。
- `更新日:` `YYYY-MM-DD`

推奨: `対象:` 一行。任意: `関連:` パス列挙。

**Status / 文書状態はメタに書かない**（フォルダで表す）。

### タスク表（`tasks` / `backlog`）

固定ヘッダ:

```text
| ID | 状態 | タスク | 優先度 | 依存 | 完了条件 | 備考 |
```

- 行の状態は **`状態` 列のみ**（ID やタスク文に「（完了）」を埋め込まない）
- 空欄禁止、不明は `-`
- 表の直前にスコープを 1 行で書く
- 親表・チケット表との同期を意識する

### 補足

- 既存ファイルの一括リネームやメタ付与は強制しない（本質更新時に寄せる）。
- 合意前の長文議論は `20_discussion/`。確定後は `30_specs` / `40_plans` / `50_tasks` の正本へ反映。

---

## SonarQube MCP Instructions

### Basic usage

- **IMPORTANT**: When starting a new task, you MUST disable automatic analysis with the `toggle_automatic_analysis` tool if it exists.
- **IMPORTANT**: When you are done generating code at the very end of the task, you MUST re-enable automatic analysis with the `toggle_automatic_analysis` tool if it exists.

### Project Keys

- When a user mentions a project key, use `search_my_sonarqube_projects` first to find the exact project key.
- Don't guess project keys - always look them up.

### Code Language Detection

- When analyzing code snippets, try to detect the programming language from the code syntax.
- If unclear, ask the user or make an educated guess based on syntax.

### Branch and Pull Request Context

- Many operations support branch-specific analysis.
- If user mentions working on a feature branch, include the branch parameter.

### Code Issues and Violations

- After fixing issues, do not attempt to verify them using `search_sonar_issues_in_projects`, as the server will not yet reflect the updates.

### Common Troubleshooting

**Authentication Issues:**

- SonarQube requires USER tokens (not project tokens).
- When the error `SonarQube answered with Not authorized` occurs, verify the token type.

**Project Not Found:**

- Use `search_my_sonarqube_projects` to find available projects.
- Verify project key spelling and format.

**Code Analysis Issues:**

- Ensure programming language is correctly specified.
- Remind users that snippet analysis doesn't replace full project scans.
- Provide full file content for better analysis results.
