# services/ 別リポジトリ移行ガイド

## 現状の全体構成

```
statevia (現リポジトリ)
├── .gitignore              # リポジトリルート（.NET 等共通）
├── core/                   # C# ワークフローエンジン（.NET）
│   ├── .gitignore          # core 配下用 .NET
│   ├── statevia.sln        # .NET ソリューション（services 非参照）
│   ├── src/
│   │   ├── Statevia.Core/  # エンジンコア（FSM / Fork-Join / ExecutionGraph）
│   │   └── Statevia.Cli/   # CLI
│   ├── tests/              # C# 単体テスト
│   └── samples/            # C# サンプル（hello-statevia）
├── docs/                   # 仕様・設計ドキュメント（エンジン + ディレクトリ構成）
└── services/               # Node/TypeScript スタック（独立）
    ├── .gitignore          # services 共通（Node 用）
    ├── docker-compose.yml  # postgres + core-api
    └── core-api/           # Express API（Execution/Node の HTTP API、DDD 構成）
        └── .gitignore      # core-api 配下用 Node
```

### 重要なポイント

- **技術スタック**: ルートは .NET（C#）、services は Node/TypeScript。ビルド・実行が完全に分離している。
- **コード依存**: core-api は C# エンジンを参照していない。Execution/Node/EventStore は TypeScript 側で独自実装。
- **ソリューション**: `statevia.sln` に services は含まれておらず、.NET のみ。
- **ドキュメント**: `docs/directory.md` に services の構成が記載されている。README の "Repository Structure" には services は未記載。

---

## 別リポジトリ化の妥当性

| 観点 | 評価 |
|------|------|
| 責務の分離 | エンジン（ライブラリ）と API（HTTP サービス）は役割が明確に分かれる。 |
| ビルド・CI | .NET と Node で別々のパイプラインにでき、シンプルになる。 |
| リリース | エンジンのバージョンと API のバージョンを独立して管理できる。 |
| チーム | フロント/API 担当とエンジン担当でリポジトリを分けやすい。 |

**結論**: services を別リポジトリに移す構成は妥当。現状の結合度は低い。

---

## 推奨: 新リポジトリの構成

現時点で `services/` 内は **core-api のみ** のため、次のいずれかが考えられる。

### 案 A: 単一サービスリポジトリ（推奨）

新リポジトリの **ルート = core-api の中身** にする。

```
statevia-api/               # 新リポジトリ名例
├── .github/workflows/      # Node 用 CI（test, build, sonar 等）
├── .gitignore              # 現 services/.gitignore を流用
├── README.md               # 概要・起動方法・statevia 本リポへのリンク
├── docker-compose.yml      # 現 services/docker-compose.yml をルートに
├── .env.example            # 現 core-api/.env.example
├── package.json
├── tsconfig.json
├── Dockerfile
├── src/
│   ├── server.ts
│   ├── domain/
│   ├── application/
│   ├── infrastructure/
│   └── presentation/
└── sql/
    └── 001_init.sql
```

- **メリット**: シンプル。1 サービス 1 リポで分かりやすい。
- **将来**: 別サービス（例: worker, auth）ができたら、その時点で別リポにするか、monorepo 化するか検討すればよい。

### 案 B: services フォルダを維持する構成

新リポジトリでも `services/` を残し、その下に core-api を置く。

```
statevia-services/
├── .github/workflows/
├── README.md
├── docker-compose.yml      # ルートに置き、context を services/core-api に
└── services/
    └── core-api/
        ├── package.json
        ├── src/
        └── ...
```

- **メリット**: 将来 `services/worker-api` などを追加しやすい。
- **デメリット**: 現状はサービスが 1 つだけなので、ディレクトリが 1 段余分になる。

**推奨**: 現状は **案 A** で進め、サービスが増えたタイミングで案 B や monorepo を検討するのがよい。

---

## 移行時にやること一覧

### 新リポジトリ側

1. **リポジトリ作成**  
   例: `statevia-api` など。空で作成。

2. **コードの移動**  
   - 案 A: `services/core-api/*` を新リポのルートにコピー（`core-api/` をはがす）。  
   - `services/docker-compose.yml` を新リポのルートにコピーし、`./core-api` 参照を `.` に変更。

3. **.gitignore**  
   - 現 `services/.gitignore` を新リポのルート `.gitignore` として使う。

4. **README.md**  
   - 役割（Execution/Node の HTTP API）、起動方法（`docker-compose up`、`npm run dev`）、環境変数（`.env.example`）、  
     および「エンジン本体は [statevia](本リポ URL) を参照」と記載。

5. **CI（例: GitHub Actions）**  
   - Node 用: `npm ci` → `npm run build`、テストがあれば実行、必要なら SonarQube。

6. **SonarQube**  
   - 本リポで core-api 用プロジェクトがある場合は、新リポ用のプロジェクトキーに切り替え。

### statevia 本リポ側

1. **services/ の削除**  
   - 移行完了後、`services/` フォルダを削除。

2. **docs/directory.md の更新**  
   - `services/` 以下の記述を削除または縮小し、  
     「HTTP API は別リポジトリ [statevia-api](新リポ URL) を参照」と記載。

3. **README.md の更新（任意）**  
   - "Repository Structure" や「使い方」に、API を使う場合は statevia-api リポを参照する旨を 1 行追加するとよい。

4. **.gitignore**  
   - ルートは .NET 用のままでよい。services 用の node 系は新リポ側で完結するため変更不要。

---

## 本リポの構成最適化（移行後）

- **statevia**: エンジンライブラリ＋CLI＋サンプル＋ドキュメントに専念。Repository Structure は `core/`（`src/`, `tests/`, `samples/`, `statevia.sln`）と `docs/` のまま。
- **statevia-api（新）**: Execution/Node の HTTP API と docker-compose（postgres + API）に専念。

これにより、「ライブラリ」と「API サービス」の責務とリポジトリが一致し、ビルド・CI・リリースの最適化がしやすくなる。

---

## 補足: 共有コードが必要になった場合

将来、Definition の型定義やイベント名などを C# と TypeScript で共有したくなった場合の選択肢:

- 仕様を `docs/`（本リポ）に置き、両リポから「仕様書」として参照する。
- 共有したい型だけ JSON Schema や OpenAPI で切り出し、本リポまたは別の「仕様リポ」で管理し、statevia-api はその生成コードを利用する。

現状はコード共有がないため、移行時には必須ではない。

---

# 代替案: C# 関連を core/ にまとめる（同一リポ内整理）

services を別リポにしない場合、または「まずリポ内で役割をはっきりさせたい」場合に、**C# 関連のフォルダを `core/` にまとめる**案が有効です。

## 案の内容

次のように移動する。

| 移動元（ルート直下） | 移動先 |
|----------------------|--------|
| `src/`               | `core/src/` |
| `tests/`             | `core/tests/` |
| `samples/`           | `core/samples/` |
| （任意）`sonar` 等   | `core/` 配下で管理 |

**sonar について**: 現状リポジトリには `sonar/` フォルダはコミットされておらず、`.gitignore` で除外されている（SonarQube の出力用）。C# 用の Sonar 設定（`sonar-project.properties` や CI 内のパス）を置く場合は、`core/sonar-project.properties` のように `core/` 配下に置くと一貫する。

## 移行後のルート構成

```txt
statevia/
├── core/                  # C# エンジン一式
│   ├── src/
│   │   ├── Statevia.Core/
│   │   └── Statevia.Cli/
│   ├── tests/
│   ├── samples/
│   └── (任意) sonar-project.properties 等
├── docs/
├── services/              # Node/TypeScript API
├── statevia.sln           # パスだけ core\... に更新
├── .gitignore
├── .editorconfig
└── README.md
```

## メリット

- **対称性**: `core/`（C# エンジン）と `services/`（API）が同列になり、「エンジン」と「サービス」の境界が一目で分かる。
- **将来の分離**: 後から core を別リポに切り出す場合、`core/` をそのままルートにして持っていきやすい。
- **ルートのスリム化**: ルート直下のフォルダが減り、docs / 設定ファイル / 各スタックの入口が整理される。
- **CI の見通し**: C# のビルド・テスト・Sonar を「core 配下」に限定して書ける。

## 変更が必要なファイル

| ファイル | 変更内容 |
|----------|----------|
| **statevia.sln** | 全プロジェクトパスを `core\src\...`, `core\tests\...`, `core\samples\...` に変更。Solution Folder "samples" のパスも `core\samples` に。 |
| **各 .csproj**   | **変更不要**。`tests` → `src`、`samples` → `src` の相対パスは `..\..\src\...` のまま、core 内で成立する。 |
| **docs/directory.md** | `src/` → `core/src/`、`tests/` → `core/tests/`、`samples/` → `core/samples/` に合わせて記述を更新。 |
| **README.md**（任意） | "Repository Structure" で `core/` を明示。 |
| **.github/workflows**（未作成） | 作成する場合はビルドパスを `core/` 基準に。 |
| **SonarQube**（利用時） | プロジェクトルートを `core` にした解析や、`sonar-project.properties` のパスを core 配下に合わせる。 |

## .sln のパス変更例

```diff
-Project("...") = "Statevia.Core", "src\Statevia.Core\Statevia.Core.csproj", ...
+Project("...") = "Statevia.Core", "core\src\Statevia.Core\Statevia.Core.csproj", ...

-Project("...") = "Statevia.Core.Tests", "tests\Statevia.Core.Tests\Statevia.Core.Tests.csproj", ...
+Project("...") = "Statevia.Core.Tests", "core\tests\Statevia.Core.Tests\Statevia.Core.Tests.csproj", ...

-Project("...") = "samples", "samples", ...
+Project("...") = "samples", "core\samples", ...

-Project("...") = "hello-statevia", "samples\hello-statevia\hello-statevia.csproj", ...
+Project("...") = "hello-statevia", "core\samples\hello-statevia\hello-statevia.csproj", ...
```

## まとめ

- **core/ にまとめる案はおすすめできる**。変更は主に .sln とドキュメントだけで、.csproj の相対参照はそのままでよい。
- **services 別リポ** と **core/ 整理** は両立可能: 先に core/ に整理してから services を別リポにしても、逆の順でもよい。core/ 化すると「エンジン本体の塊」が明確になり、どちらの作業もやりやすくなる。

---

## 発展案: .sln を core/ に移動 + .gitignore を 3 箇所に配置

「C# 関連を core/ にまとめる」をさらに進め、**statevia.sln も core/ に置く**うえで、**.gitignore をルート・core・services/core-api の 3 箇所に役割分担して配置する**案です。

### 構成イメージ

```txt
statevia/
├── .gitignore              # リポジトリ全体（IDE / OS / Sonar 等）
├── .editorconfig
├── README.md
├── docs/
│
├── core/                    # C# エンジン一式（ここが .NET のルート）
│   ├── .gitignore           # C# / .NET 用
│   ├── statevia.sln         # ソリューションは core 直下
│   ├── src/
│   ├── tests/
│   └── samples/
│
└── services/
    └── core-api/
        ├── .gitignore       # Node / TypeScript 用
        ├── package.json
        ├── src/
        └── ...
```

### メリット

- **.sln を core に置く**: C# を触るときは `core/` を開けばよく、.sln 内のパスは **core 基準**で `src\...`, `tests\...`, `samples\...` のまま書ける（ルートに `core\` プレフィックスは不要）。Visual Studio で `core/statevia.sln` を開く運用になる。
- **責務の明確化**: ルートは「リポ全体の共通除外」、core は「.NET ビルド成果物・ツール」、services/core-api は「Node 依存・ビルド・env」と分けられる。
- **将来の分離**: core を別リポにするときは `core/` をそのままルートにすればよく、.sln もそのまま使える。services 側も `services/core-api/` を別リポのルートにしやすい。
- **重複の削減**: 各スタックの .gitignore にだけそのスタック用のルールを書け、ルートでは共通のものだけを扱える。

### .sln を core に移したときのパス

.sln を **core/statevia.sln** に置く場合、相対パスは .sln の場所（core/）基準になるため、**現在のまま**でよい。

- `src\Statevia.Core\Statevia.Core.csproj`
- `src\Statevia.Cli\Statevia.Cli.csproj`
- `tests\Statevia.Core.Tests\...`
- `tests\Statevia.Cli.Tests\...`
- `samples\hello-statevia\...`

**必要な作業**: statevia.sln をルートから `core/statevia.sln` に**ファイルごと移動**するだけ。中身のパスは変更不要。ルートでソリューションを開いていた CI やドキュメントは「`core/statevia.sln` を開く／ビルドする」に更新する。

### .gitignore の 3 箇所の役割と内容案

| 配置 | 役割 | 記載する内容の例 |
|------|------|------------------|
| **リポジトリルート** | リポ全体で共通の除外 | IDE（.cursor/, .vscode/, .idea/）、Sonar（.sonarqube/, .sonarlint/, sonar/）、OS（.DS_Store, Thumbs.db）、一時ファイル（~$*, *~） |
| **core/** | C# / .NET のみ | ビルド結果（[Bb]in/, [Oo]bj/, [Dd]ebug/, [Rr]elease/）、.vs/, NuGet（*.nupkg）、project.lock.json、*.binlog、TestResult.xml、coverage/ 等 |
| **services/core-api/** | Node / TypeScript のみ | node_modules/, dist/, *.tsbuildinfo, .env 系、logs/, coverage/, .nyc_output/ 等（現 services/.gitignore の内容） |

このように分けると、ルートの .gitignore は「どのスタックでも使うもの」に絞れ、core と services/core-api はそれぞれのツールチェーンに特化したルールだけを持つ形になる。

### 移行時の具体的な手順

1. **core/ を作成し、src / tests / samples を移動**（前述のとおり。.csproj は変更不要。）
2. **statevia.sln を core/ に移動**  
   - ルートの `statevia.sln` を `core/statevia.sln` に移動。  
   - .sln 内のパスはそのままでよい（core 相対で src/tests/samples になる）。
3. **ルート .gitignore を「共通のみ」に整理**  
   - .NET 専用の行（[Bb]in/, [Oo]bj/, .vs/, *.nupkg 等）を削除し、上表の「ルート」に書いたような共通項目だけ残す。
4. **core/.gitignore を新規作成**  
   - 上表の「core」の内容（現在ルートにあった .NET 用のルール）を core/.gitignore に書く。
5. **services/core-api/.gitignore を新規作成**  
   - 現在の `services/.gitignore` の内容を `services/core-api/.gitignore` にコピー。  
   - `services/.gitignore` は削除してよい（core-api 以外にサービスがなければ）。将来 services 下に別サービスを増やす場合は、そのサービスごとに .gitignore を置くか、services に「Node 共通」の軽い .gitignore を残すかは任意。
6. **docs/directory.md と README**  
   - ソリューションの場所を「core/statevia.sln」に、.gitignore を「ルート・core・services/core-api」の 3 箇所に更新。

### まとめ

- **statevia.sln を core に移し、.gitignore をルート・core・services/core-api の 3 箇所に分ける案は、運用と将来のリポ分離の両方に有利**です。
- .sln は「core の入口」にまとまり、.gitignore は責務ごとに分かれて読みやすく、他スタックを追加するときも同じパターンで拡張しやすいです。
