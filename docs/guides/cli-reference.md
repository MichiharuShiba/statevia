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

Action Module zip を **テナント別** modules ディレクトリへ安全に展開します。展開先は `{modulesRoot}/{tenantKey}/` です（`--tenant` 必須）。

```bash
dotnet run --project Statevia.Service.Cli -- module install ./my-module.zip \
  --modules-path ./modules \
  --tenant default \
  --api-base http://localhost:8080 \
  --token "<bearer-token>"
```

| オプション | 説明 |
| --- | --- |
| `zip-file` | インストールする zip |
| `--tenant`, `-T` | **必須**。展開先 `{modulesRoot}/{tenantKey}/` と reload の `X-Tenant-Id` |
| `--modules-path`, `-m` | modules ルート（既定: `STATEVIA_MODULES_PATH` または `./modules`） |
| `--api-base`, `-a` | 任意。指定時は install 後に reload API を呼ぶ |
| `--token`, `-t` | reload 用 Bearer トークン |
| `--skip-reload` | reload をスキップ |

- `--tenant` 省略・不正キー（`..` 等）は非ゼロ終了。ルート直下への展開はしない。
- reload のテナントは install の `--tenant` のみ（別フラグなし）。非 2xx は非ゼロ。Token は標準出力に出さない。
- 本コマンドは **運用者／デプロイ向け**（modules 書き込み＝ホスト信頼境界）。テナント存在確認や容量上限は未実装。SaaS 向け強化は別 Spec（`saas-module-tenant-controls`）。
- Git / S3 / OCI からの取得 CLI は未対応（プロセスグローバル Source 設定とは別）。

zip レイアウトは [module-zip-layout 仕様](../specifications/actions/module-zip-layout.md) を参照。

## 次に読むもの

- Engine 単体: [engine-standalone-guide.md](engine-standalone-guide.md)
- Module 署名: [action-module-signing.md](action-module-signing.md)
