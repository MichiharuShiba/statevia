# CLI リファレンス

| 項目 | 値 |
| --- | --- |
| 種別 | Guide |
| Version | 1.3 |
| 更新日 | 2026-08-16 |
| 関連 | [../../service/cli/Statevia.Service.Cli/](../../service/cli/Statevia.Service.Cli/) |

---

統合 CLI **`statevia`**（`service/cli/Statevia.Service.Cli`）のサブコマンド一覧。

## 実行方法

本ガイドの例は、PATH 上の **`statevia`** を前提にします。

```bash
statevia <subcommand> [options]
```

ランタイム同梱の単一ファイルは、リポジトリルートから次で出力します。Windows（x64）の例です。

```powershell
dotnet publish service/cli/Statevia.Service.Cli/Statevia.Service.Cli.csproj `
  -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\artifacts\statevia
```

成果物は `artifacts/statevia/statevia.exe` です。このフォルダを PATH に入れるか、フルパスで実行します。Linux / macOS では `-r` を `linux-x64` または `osx-arm64` に変えます（ファイル名は `statevia`）。

exe を使わない場合は `dotnet run` でも同じサブコマンドを呼べます。`--` 以降が CLI 引数です。

```bash
cd service/cli
dotnet run --project Statevia.Service.Cli -- <subcommand> [options]
```

`dotnet build -c Release` だけでも `bin/Release/net8.0/statevia.exe` は出ますが、同じフォルダの DLL と **.NET 8 ランタイム**が必要です。

## `definition validate`

ワークフロー定義 YAML を Engine Loader で読み込み、検証します。

```bash
statevia definition validate path/to/workflow.yaml
```

| 引数 | 説明 |
| --- | --- |
| `yaml-file` | 検証対象の YAML ファイル |

終了コード `0` で成功。構文・セマンティクスエラーは stderr に出力されます。

## `auth login` / `auth logout`

Service API にログインし、資格情報を **`~/.statevia/credentials`**（環境変数 `STATEVIA_CREDENTIALS_FILE` で上書き可）へ保存します。Unix ではファイル権限を所有者読み書き（0600）にします。トークンは標準出力に出しません。

```bash
statevia auth login \
  --api-base http://localhost:8080 \
  --tenant default \
  --email ops@example.com
# --password を省略するとプロンプトで入力する

statevia auth logout
```

| オプション（login） | 説明 |
| --- | --- |
| `--api-base`, `-a` | **必須**。Service API の基底 URL |
| `--tenant`, `-T` | **必須**。テナントキー |
| `--email`, `-e` | **必須**。ログインメール |
| `--password`, `-p` | パスワード（省略時は標準入力） |

期限切れの JWT では `module install` の reload が失敗し、再ログインを促します。

## `module install`

Action Module zip を **テナント別** modules ディレクトリへ安全に展開します。展開先は `{modulesRoot}/{tenantKey}/` です（`--tenant` 必須）。

reload する場合は、先に `auth login` するか、スコープ `modules.reload` の API キーを使います。

```bash
statevia auth login \
  --api-base http://localhost:8080 \
  --tenant default \
  --email ops@example.com

statevia module install ./my-module.zip \
  --modules-path ./modules \
  --tenant default \
  --api-base http://localhost:8080
```

API キー（CI 向け）:

```bash
statevia module install ./my-module.zip \
  --modules-path ./modules \
  --tenant default \
  --api-base http://localhost:8080 \
  --api-key "<modules.reload-scoped-key>"
```

| オプション | 説明 |
| --- | --- |
| `zip-file` | インストールする zip |
| `--tenant`, `-T` | **必須**。展開先 `{modulesRoot}/{tenantKey}/` と reload の `X-Tenant-Id` |
| `--modules-path`, `-m` | modules ルート（既定: `STATEVIA_MODULES_PATH` または `./modules`） |
| `--api-base`, `-a` | 任意。指定時は install 後に reload API を呼ぶ |
| `--api-key` | reload 用 API キー（`X-Api-Key`）。`STATEVIA_API_KEY` でも可 |
| `--token`, `-t` | **非推奨**。reload 用 Bearer トークン。このリリースでは動作する |
| `--skip-reload` | reload をスキップ |

- `--tenant` 省略・不正キー（`..` 等）は非ゼロ終了。ルート直下への展開はしない。
- reload のテナントは install の `--tenant` のみ（別フラグなし）。非 2xx は非ゼロ。トークンと API キーは標準出力に出さない。
- reload 認証の優先順位: `--api-key` / `STATEVIA_API_KEY` → `auth login` の資格情報 → `--token`（非推奨）。
- 本コマンドは **運用者／デプロイ向け**（modules 書き込み＝ホスト信頼境界）。テナント存在確認や容量上限は未実装。SaaS 向け強化は別 Spec（`saas-module-tenant-controls`）。
- Git / S3 / OCI からの取得 CLI は未対応（プロセスグローバル Source 設定とは別）。

zip レイアウトは [module-zip-layout 仕様](../specifications/actions/module-zip-layout.md) を参照。

## 次に読むもの

- Engine 単体: [engine-standalone-guide.md](engine-standalone-guide.md)
- Module 署名: [action-module-signing.md](action-module-signing.md)
