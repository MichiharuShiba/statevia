# U5: api から engine の参照パスと CI ビルド議論

**前提**（決定）: **engine/** と **api/** はリポジトリ直下に配置し、**別ソリューション**とする（リポジトリ直下に .sln を置かない）。api は engine を**プロジェクト参照**する（開発時。将来は NuGet 想定）。

このドキュメントでは **api の csproj から engine への ProjectReference の相対パス** と **ルートに .sln を置かない場合の CI ビルド** を議論する。

---

## 1. ディレクトリ構造の確認

```text
（リポジトリルート）
├── engine/
│   ├── Statevia.Core.Engine/
│   │   └── Statevia.Core.Engine.csproj
│   ├── Statevia.Core.Engine.Tests/
│   └── statevia-engine.sln  （例。engine 用）
├── api/
│   ├── Statevia.Core.Api/
│   │   └── Statevia.Core.Api.csproj   ← ここから engine を参照
│   ├── Statevia.Core.Api.Tests/
│   └── statevia-api.sln  （例。api 用）
├── services/
│   └── ui/
└── （ルートには .sln を置かない）
```

- **Statevia.Core.Api.csproj** から **Statevia.Core.Engine.csproj** への相対パスは、  
  `api/Statevia.Core.Api/` から見て `engine/Statevia.Core.Engine/` へ行くには、  
  いったん **api** の上（リポジトリルート）に出てから **engine** に入る必要がある。

---

## 2. ProjectReference の相対パス

### 2.1 相対パスの候補

**Statevia.Core.Api.csproj**（`api/Statevia.Core.Api/Statevia.Core.Api.csproj`）に書く ProjectReference の例:

| 候補 | パス                                                                 | 説明                                                                                |
| ---- | -------------------------------------------------------------------- | ----------------------------------------------------------------------------------- |
| A    | `../../engine/Statevia.Core.Engine/Statevia.Core.Engine.csproj`      | csproj からの相対。2 階層上が api/ の親（ルート）、その後 engine/... へ。           |
| B    | `$(RepoRoot)engine/Statevia.Core.Engine/Statevia.Core.Engine.csproj` | RepoRoot を Directory.Build.props などで定義する場合。                              |
| C    | その他                                                               | ルートに nuget.config や Directory.Build.props を置き、共通の変数でパスを揃える等。 |

**推奨**: シンプルに **A**（`../../engine/Statevia.Core.Engine/Statevia.Core.Engine.csproj`）でよい。

- 条件: api の .sln でビルドするとき、この csproj が `api/Statevia.Core.Api/` にあり、engine が `engine/Statevia.Core.Engine/` にある前提。
- **注意**: api の .sln が api 配下にある場合、`dotnet build` のカレントディレクトリが `api/` であっても、csproj 内の相対パスは **その csproj ファイルの位置** を基準に解決される。したがって `../../engine/...` は「api/Statevia.Core.Api/ から 2 階層上 = ルート」→「engine/Statevia.Core.Engine/」を指す。

---

## 3. ソリューションの配置とビルド順

### 3.1 engine 用 .sln と api 用 .sln がそれぞれ配下にある場合

- **engine/statevia-engine.sln**（例）: Statevia.Core.Engine と Statevia.Core.Engine.Tests を含む。engine 単体でビルド可能。
- **api/statevia-api.sln**（例）: Statevia.Core.Api と Statevia.Core.Api.Tests を含み、Statevia.Core.Api が engine を ProjectReference（上記パス）で参照。
- api の .sln をビルドすると、MSBuild が ProjectReference を解決するため、**engine の csproj が自動的にビルドされる**（engine を先にビルドする必要は、通常はない。ただし engine の .sln には api は含めない）。

### 3.2 CI でのビルド方法の選択肢

| 案                                                       | 内容                                                                                                                                             | メリット                                                                    | デメリット                                                                                                                     |
| -------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------ | --------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| **A: api の .sln のみビルド**                            | CI で `dotnet build api/statevia-api.sln`（または api/ 配下の .sln）を実行。ProjectReference で engine が解決され、engine も一緒にビルドされる。 | 手順が 1 本。engine の変更も api ビルドで検知される。                       | engine 単体のビルドは別ジョブで行わない限り、engine だけのビルド結果は明示的には出ない。                                       |
| **B: engine を先にビルドし、続けて api をビルド**        | 例: `dotnet build engine/statevia-engine.sln` → `dotnet build api/statevia-api.sln`。                                                            | engine 単体のビルド結果を先に確認できる。api は engine が通った後にビルド。 | ステップが 2 つ。                                                                                                              |
| **C: ルートに「統合 .sln」を 1 つだけ用意（CI 専用等）** | リポジトリ直下に statevia.sln を置き、engine と api の両方のプロジェクトを含める。CI では `dotnet build statevia.sln`。                          | 1 コマンドで全体ビルド。                                                    | 「リポジトリ直下に .sln を置かない」という方針と矛盾する。方針を「CI 用にはルートに 1 つおいてよい」などに変更する必要がある。 |

・**推奨の方向性（議論用）**

- 通常は **案 A**（api の .sln をビルドするだけ）で十分。ProjectReference により engine もビルドされる。
- engine 単体のテストを CI で必ず回したい場合は **案 B**（engine の .sln でビルド＆テスト → 続けて api の .sln でビルド＆テスト）。
- ルートに .sln を置くことを許容するなら **案 C** も可（その場合は「リポジトリ直下に .sln を置かない」を「日常の開発用には各配下の .sln、CI 用はルートの .sln 可」などに更新）。

---

## 4. その他の検討

### 4.1 engine の出力パス

- engine はライブラリなので、通常は api のビルド時に engine の dll が参照される。
- api の .sln からビルドする場合、engine の csproj の **OutputPath** は標準で bin/Release などになる。api は engine のビルド結果を参照するため、**特別な出力先の指定は不要**なことが多い。

### 4.2 複数 .sln の名前

- engine 用: `statevia-engine.sln`, `engine.sln`, `Statevia.Core.Engine.sln` など。
- api 用: `statevia-api.sln`, `api.sln`, `Statevia.Core.Api.sln` など。
- 命名は「どの .sln が engine 用か api 用か分かること」が条件。リポジトリ内で統一する。

### 4.3 将来 NuGet 参照に切り替える場合

- 開発時は ProjectReference、本番や他チーム向けには NuGet で Statevia.Core.Engine を参照する、という運用にする場合、**同じ csproj で条件付き参照**（例: Directory.Build.props で Release かつ環境変数があれば PackageReference、それ以外は ProjectReference）にすることもできる。
- または api を別リポジトリに分離し、そのリポジトリでは常に PackageReference、という形にする。

---

## 5. 決定事項・オープンな論点（記入用）

### 5.1 決定事項

| 事項                        | 決定内容                                                                                                                              |
| --------------------------- | ------------------------------------------------------------------------------------------------------------------------------------- |
| ProjectReference の相対パス | api の Statevia.Core.Api.csproj から engine へのパス（例: `../../engine/Statevia.Core.Engine/Statevia.Core.Engine.csproj`）（決定）。 |
| CI ビルド方法               | 案 A                                                                                                                                  |
| ルートに .sln を置くか      | 置かない                                                                                                                              |
| engine / api の .sln の名前 | statevia-engine.sln, statevia-api.sln。                                                                                               |

### 5.2 オープンな論点

- 上記のうち未決のもの。

以上を、U5 api から engine の参照パスと CI ビルドの議論のたたき台とする。
