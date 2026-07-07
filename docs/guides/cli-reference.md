# CLI リファレンス

| 項目 | 値 |
| --- | --- |
| 種別 | Guide |
| Version | 1.0 |
| 更新日 | 2026-07-07 |
| 関連 | [../../service/cli/Statevia.Service.Cli/](../../service/cli/Statevia.Service.Cli/) |

---

統合 CLI **`statevia`**（`service/cli/Statevia.Service.Cli`）のサブコマンド一覧。

## 実行方法

```bash
cd service/cli
dotnet run --project Statevia.Service.Cli -- <subcommand> [options]
```

ビルド後の実行ファイルを PATH に置いても同様です。

## `definition validate`

ワークフロー定義 YAML を Engine Loader で読み込み、検証します。

```bash
dotnet run --project Statevia.Service.Cli -- definition validate path/to/workflow.yaml
```

| 引数 | 説明 |
| --- | --- |
| `yaml-file` | 検証対象の YAML ファイル |

終了コード `0` で成功。構文・セマンティクスエラーは stderr に出力されます。

## `module install`

Action Module zip を modules ルートへ安全に展開します。

```bash
dotnet run --project Statevia.Service.Cli -- module install ./my-module.zip \
  --modules-path ./modules \
  --api-base http://localhost:8080 \
  --token "<bearer-token>" \
  --tenant default
```

| オプション | 説明 |
| --- | --- |
| `zip-file` | インストールする zip |
| `--modules-path`, `-m` | 展開先（既定: `STATEVIA_MODULES_PATH` または `./modules`） |
| `--api-base`, `-a` | 任意。指定時は install 後に reload API を呼ぶ |
| `--token`, `-t` | reload 用 Bearer トークン |
| `--tenant`, `-T` | reload 時の `X-Tenant-Id`（既定 `default`） |
| `--skip-reload` | reload をスキップ |

zip レイアウトは [module-zip-layout 仕様](../specifications/actions/module-zip-layout.md) を参照。

## 次に読むもの

- Engine 単体: [engine-standalone-guide.md](engine-standalone-guide.md)
- Module 署名: [action-module-signing.md](action-module-signing.md)
