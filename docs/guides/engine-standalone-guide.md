# Engine 単体ガイド

| 項目 | 値 |
| --- | --- |
| 種別 | Guide |
| Version | 1.0 |
| 更新日 | 2026-07-07 |
| 関連 | [../../core/engine/samples/hello-statevia/README.md](../../core/engine/samples/hello-statevia/README.md) |

---

Core-Engine は **C# ライブラリ**です。Core-API なしで、サンプルプロジェクトから定義をロードしてインメモリ実行できます。

## サンプル: hello-statevia

```bash
cd core/engine/samples/hello-statevia
dotnet run
```

`hello.yaml` に定義があり、コンソール上でワークフローが進行します。永続化・認証・Action Module は含みません。

## 定義の検証（CLI）

ホストに API を立てずに YAML の構文・セマンティクスを確認する:

```bash
cd service/cli
dotnet run --project Statevia.Service.Cli -- definition validate path/to/workflow.yaml
```

成功時はワークフロー名と状態一覧を出力します。詳細は [cli-reference.md](cli-reference.md)。

## Engine テストの実行

```bash
cd core/engine
dotnet test statevia-engine.sln
```

## Core-API 経由との違い

| 項目 | Engine 単体 | Core-API |
| --- | --- | --- |
| 永続化 | なし | PostgreSQL |
| Action Module | サンプル内のスタブ | Catalog / Policy / Host |
| HTTP | なし | `/v1/*` |
| read-model | in-memory のみ | DB projection |

本番相当の動作確認は [getting-started.md](getting-started.md) を参照してください。

## 次に読むもの

- 定義の書き方: [../concepts/definition.md](../concepts/definition.md)
- 実行モデル: [../concepts/execution-model.md](../concepts/execution-model.md)
- 定義仕様: [../specifications/definition.md](../specifications/definition.md)
