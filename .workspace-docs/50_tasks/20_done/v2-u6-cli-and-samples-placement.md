# U6: Statevia.Cli と samples の配置議論

**前提**（modification-plan / U5）: **engine/** と **api/** はリポジトリ直下に配置し、別ソリューションとする。Statevia.Core は **Statevia.Core.Engine** にリネームされ engine/ に置かれる。現行の **core/** には Statevia.Core, Statevia.Cli, Statevia.Core.Tests, Statevia.Cli.Tests, samples/hello-statevia がある。

このドキュメントでは **Statevia.Cli の配置**（engine/ に置くか、api/ に置くか、別ディレクトリか）と **samples の配置・参照先**（engine 参照のみか、api も使うか）を議論する。

---

## 1. 現行の役割の確認

### 1.1 Statevia.Cli（現行）

- **参照**: Statevia.Core のみ（DefinitionLoader, DefinitionValidator）。
- **機能**: コマンドラインから YAML ファイルを指定し、**定義の読み込みと検証**を行う。`Statevia.Cli <yaml-file>`。WorkflowEngine は使わず、HTTP も DB も使わない。
- **結論**: **Engine の Definition レイヤーだけを利用**している。API や永続化に依存しない。

### 1.2 samples/hello-statevia（現行）

- **参照**: Statevia.Core のみ（DefinitionLoader, Validators, DictionaryStateExecutorFactory, DefinitionCompiler, WorkflowEngine, DefaultStateExecutor）。
- **機能**: ローカルの hello.yaml を読み、**in-process で WorkflowEngine を起動し** Start → PublishEvent → GetSnapshot / ExportExecutionGraph まで実行するデモ。HTTP も DB も使わない。
- **結論**: **Engine を単体で使う実行サンプル**。API は使っていない。

---

## 2. Statevia.Cli の配置の選択肢

| 案                    | 配置                     | 説明                                                                                                                                                          | メリット                                                                                                                          | デメリット                                                                                                                                                  |
| --------------------- | ------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **A: engine 配下**    | `engine/Statevia.Cli/`   | Engine の .sln（statevia-engine.sln）に CLI プロジェクトを追加。CLI は Statevia.Core.Engine を ProjectReference。                                             | CLI の責務（定義の読み込み・検証）が Engine と一致。Engine 単体で「定義検証ツール」が使える。ビルドは engine の .sln のみで完結。 | engine の .sln にプロジェクトが 1 つ増える。                                                                                                                |
| **B: api 配下**       | `api/Statevia.Cli/` など | API の .sln に CLI を入れる。CLI は Engine を参照（api 経由で engine を参照する構成なら、CLI も engine を ProjectReference で参照する想定）。                 | 一見「API スタックに CLI も含める」と解釈できる。                                                                                 | CLI は API や DB を使わないため、api 配下に置く必然性が薄い。api の .sln が「REST + 永続化」以外の目的で肥大化する。                                        |
| **C: 別ディレクトリ** | `cli/`（リポジトリ直下） | engine と api と同階層に `cli/` を用意。cli/ に Statevia.Cli と statevia-cli.sln。CLI は engine を `../engine/Statevia.Core.Engine/...` で ProjectReference。 | CLI を「スタンドアロンツール」として明確に分離できる。                                                                            | ソリューションが 3 つ（engine / api / cli）になり、CI や開発手順が増える。CLI は実質 Engine の薄いラッパーなので、engine と分離する利点が小さい場合がある。 |

### Statevia.Cli の推奨（議論用）

- **案 A（engine 配下）** を推奨。CLI は「定義の読み込み・検証」のみで、すべて Engine（Statevia.Core.Engine）の API で完結している。engine の .sln に Statevia.Core.Engine, Statevia.Core.Engine.Tests, **Statevia.Cli**, **Statevia.Cli.Tests** を含めれば、Engine 単体で開発・テスト・CLI 利用ができる。
- 将来「CLI から Core-API（HTTP）を呼んで定義を登録する」などの機能を足す場合は、その時点で api 依存の別 CLI プロジェクト（例: api 配下の Cli または cli/ で api を参照）を検討すればよい。

---

## 3. samples の配置と参照先

### 3.1 配置の選択肢

| 案                               | 配置                                                                           | 説明                                                                                                                                    | メリット                                                                                                                           | デメリット                                                                                                                        |
| -------------------------------- | ------------------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------- |
| **A: engine 配下**               | `engine/samples/hello-statevia/`                                               | Engine の .sln に samples を含めるか、含めずにフォルダだけ engine/ に置く。hello-statevia は Statevia.Core.Engine を ProjectReference。 | Engine を「ライブラリとしてどう使うか」のサンプルが engine と一体で管理される。                                                    | .sln に含める場合、engine の .sln のプロジェクト数が増える。含めない場合は `dotnet run -p engine/samples/hello-statevia` で実行。 |
| **B: リポジトリ直下の samples/** | `samples/hello-statevia/`（ルートの samples）                                  | engine と api の外に、ルートで `samples/` を置く。hello-statevia は `../engine/Statevia.Core.Engine/` を ProjectReference。             | 「engine でも api でもない共通のサンプル」として整理できる。複数サンプル（engine のみ / api 利用）を同じ samples/ で管理しやすい。 | 相対パスが長くなる。CI で samples をビルドする場合はパス解決が必要。                                                              |
| **C: 複数に分ける**              | `engine/samples/`（engine のみ）, `api/samples/` または `samples/`（api 利用） | Engine 単体サンプルは engine/samples/、API を呼ぶサンプルは api 配下またはルート samples/。                                             | 参照先（engine のみ / api）がディレクトリで明確。                                                                                  | サンプルが分散し、探しづらくなる可能性。                                                                                          |

### 3.2 参照先（engine のみか、api も使うか）

- **現行の hello-statevia**: Engine 参照のみで十分。v2 移行後も **engine 参照のみ** のままでよい（Statevia.Core.Engine を参照し、in-process で WorkflowEngine を動かす）。
- **API を利用するサンプル**: 将来、「REST API に定義を POST してワークフローを開始する」などのサンプルを用意する場合は、**api を参照するか、HTTP クライアントで api のエンドポイントを叩く**形になる。その場合は api 配下の samples またはルートの samples/ に置く選択肢がある。
- **本 U6 のスコープ**: まずは **既存の hello-statevia を engine 参照のみでどこに置くか** を決め、API 利用サンプルは「必要になったら追加し、その時に配置を決める」でよい。

### samples の推奨（議論用）

- **samples は engine 配下**（案 A: `engine/samples/hello-statevia/`）を推奨。hello-statevia は Engine 単体の利用例なので、engine と一緒に置くと分かりやすい。engine の .sln には **含めない**（オプションで .sln に含めてもよい）。ビルドは `dotnet build engine/Statevia.Core.Engine` が通っていれば、`dotnet run -p engine/samples/hello-statevia` で実行可能。
- 将来 API 利用サンプルを追加する場合は、`api/samples/` またはルート `samples/` に「api クライアント用サンプル」を追加する形で検討。

---

## 4. リネーム・参照パスの整理

- **Statevia.Cli**: プロジェクト名は **Statevia.Cli** のままとするか、**Statevia.Cli** のまま（Engine 配下に置くだけ）でよい。名前空間は現行どおり `Statevia.Cli` でよい。
- **Statevia.Cli の Engine 参照**: engine 配下に置く場合、`Statevia.Cli.csproj` は `../Statevia.Core.Engine/Statevia.Core.Engine.csproj` で ProjectReference（engine の .sln 内での相対パス）。
- **Statevia.Cli.Tests**: CLI を engine 配下に置く場合、**Statevia.Cli.Tests** も engine 配下（`engine/Statevia.Cli.Tests/`）に移し、statevia-engine.sln に含める。
- **samples/hello-statevia**: engine 配下に移す場合、`../../Statevia.Core.Engine/Statevia.Core.Engine.csproj` のような相対パスで Engine を参照（`engine/samples/hello-statevia/` から `engine/Statevia.Core.Engine/`）。

---

## 5. Phase 1 との整合

modification-plan の **Phase 1** では「Core（C#）を Core-Engine（C#）として明確化する」として、1.1 で「プロジェクトのリネーム」「engine/ 配下に配置」、1.2 で「依存の整理」、1.3 で「名前空間」とある。

- **CLI と samples の移行**は、Phase 1 の「engine/ 配下に配置」に含めてよい。つまり 1.1 のタスクに「Statevia.Cli と Statevia.Cli.Tests を engine/ に移す」「samples/hello-statevia を engine/samples/ に移す」を追記する。
- または Phase 1 の「成果物」の直後にある **別タスク** として「CLI と samples の engine への移行」を明示してもよい。

---

## 6. 決定事項・オープンな論点（記入用）

### 6.1 決定事項

| 事項                | 決定内容                                                                                |
| ------------------- | --------------------------------------------------------------------------------------- |
| Statevia.Cli の配置 | engine 配下。statevia-engine.sln に Statevia.Cli と Statevia.Cli.Tests を含める（決定） |
| samples の配置      | engine/samples/hello-statevia。engine 参照のみ。.sln には含めない（決定）               |
| API 利用サンプル    | 今回のスコープ外。必要になったら api/samples または samples/ で検討（決定）             |

### 6.2 オープンな論点

- 特になし。CLI は .sln に含める、samples は .sln に含めない、で決定済み。

以上を、U6 Statevia.Cli と samples の配置の決定とする。
