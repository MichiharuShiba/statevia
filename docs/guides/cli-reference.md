# CLI リファレンス

| 項目 | 値 |
| --- | --- |
| 種別 | Guide |
| Version | 1.6 |
| 更新日 | 2026-08-18 |
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

## ホームディレクトリ（`STATEVIA_HOME`）

CLI の非秘密設定・JWT・API キーは **`{UserProfile}/.statevia`** に置きます。環境変数 `STATEVIA_HOME` でホームディレクトリを差し替えられます（テスト／CI の隔離用）。

```text
{UserProfile}/.statevia/     # または STATEVIA_HOME
  config                     # 非秘密 JSON（apiBase, tenant, modulesPath）
  credentials                # JWT（auth login）
  secrets                    # API キーのみ
```

作業ディレクトリ相対の `.statevia/` は **使いません**。同じ名前のファイルがあっても読みません。

設定値（`api-base` / `tenant` / `modules-path`）の優先順位は **フラグ > 対応する環境変数 > ホーム `config`** です。

| 環境変数 | 対象 |
| --- | --- |
| `STATEVIA_HOME` | ホームディレクトリ |
| `STATEVIA_API_BASE` | `api-base` |
| `STATEVIA_TENANT` | `tenant` |
| `STATEVIA_MODULES_PATH` | `modules-path` |
| `STATEVIA_API_KEY` | API キー（ホーム `secrets` より優先） |
| `STATEVIA_CREDENTIALS_FILE` | JWT ファイルの絶対パス上書き（移行用） |

## `definition validate`

ワークフロー定義 YAML を Engine Loader で読み込み、検証します。

```bash
statevia definition validate path/to/workflow.yaml
```

| 引数 | 説明 |
| --- | --- |
| `yaml-file` | 検証対象の YAML ファイル |

終了コード `0` で成功。構文・セマンティクスエラーは stderr に出力されます。

## `config set` / `config get` / `config unset`

ホームの設定を読み書きします。書き先は常に `STATEVIA_HOME` です。

```bash
statevia config set
# 引数なし: api-base / tenant / modules-path / api-key を順に尋ねる
# 既存値は初期値として表示（API キーは ********）。Enter で維持、入力で上書き

statevia config set api-base http://localhost:8080
statevia config set tenant
# キーのみ: その項目だけ尋ねる

statevia config set api-key
# TTY では入力中を * でマスクする（標準出力に実値は出さない）

statevia config get
statevia config get tenant
statevia config unset api-key
```

| キー | 書き先 |
| --- | --- |
| `api-base` / `tenant` / `modules-path` | `{STATEVIA_HOME}/config` |
| `api-key` | `{STATEVIA_HOME}/secrets`（Unix は 0600） |

対話の初期値はホームに保存済みの値です（環境変数の実効値はファイルへ書き戻しません）。値の削除は `unset` です。

`get` は優先順位適用後の実効値です。未設定は `(unset)`。API キーは設定済みなら `********`。JWT は出しません。

## `auth login` / `auth logout`

Service API にログインし、資格情報を **`{STATEVIA_HOME}/credentials`**（環境変数 `STATEVIA_CREDENTIALS_FILE` で上書き可）へ保存します。Unix ではファイル権限を所有者読み書き（0600）にします。トークンは標準出力に出しません。

`--api-base` / `--tenant` はホーム `config` または環境変数があれば省略できます。成功時は解決した `apiBase` / `tenant` をホーム `config` へ書き戻します。

```bash
statevia auth login \
  --api-base http://localhost:8080 \
  --tenant default \
  --username ops
# --password を省略するとプロンプトで入力する

statevia auth logout
```

| オプション（login） | 説明 |
| --- | --- |
| `--api-base`, `-a` | Service API の基底 URL。省略時は `STATEVIA_API_BASE` またはホーム `config` |
| `--tenant`, `-t` | テナントキー。省略時は `STATEVIA_TENANT` またはホーム `config` |
| `--username`, `-u` | **必須**。ログインユーザー名（1〜64 文字。先頭・末尾は英数字。途中のみハイフン・アンダースコア・ドット） |
| `--password`, `-p` | パスワード（省略時はプロンプト。TTY では入力中を `*` でマスク） |

どの源泉にも `api-base` / `tenant` が無いときは従来どおり必須エラーです。期限切れの JWT では `module install` の reload が失敗し、再ログインを促します。

## `module install`

Action Module zip を **テナント別** modules ディレクトリへ安全に展開します。展開先は `{modulesRoot}/{tenantKey}/` です。

reload する場合は、先に `auth login` するか、スコープ `modules.reload` の API キーを使います。API キーは `--api-key` / `STATEVIA_API_KEY` のほか、ホーム `secrets` からも読みます。

```bash
statevia config set api-base http://localhost:8080
statevia config set tenant default
statevia auth login --username ops

statevia module install ./my-module.zip \
  --modules-path ./modules \
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
| `--tenant`, `-t` | 展開先 `{modulesRoot}/{tenantKey}/` と reload の `X-Tenant-Id`。省略時は `STATEVIA_TENANT` またはホーム `config` |
| `--modules-path`, `-m` | modules ルート（既定: フラグ / `STATEVIA_MODULES_PATH` / ホーム `config` / `./modules`） |
| `--api-base`, `-a` | 任意。指定時（またはホーム `config` / `STATEVIA_API_BASE`）は install 後に reload API を呼ぶ |
| `--api-key` | reload 用 API キー（`X-Api-Key`）。`STATEVIA_API_KEY` およびホーム `secrets` でも可 |
| `--token` | **非推奨**。reload 用 Bearer トークン。このリリースでは動作する（短い別名なし） |
| `--skip-reload` | reload をスキップ |

- `--tenant` がどの源泉にも無い・不正キー（`..` 等）は非ゼロ終了。ルート直下への展開はしない。
- reload のテナントは install で解決したテナントのみ（別フラグなし）。非 2xx は非ゼロ。トークンと API キーは標準出力に出さない。
- reload 認証の優先順位: `--api-key` / `STATEVIA_API_KEY` → ホーム `secrets` → `auth login` の資格情報 → `--token`（非推奨）。
- 本コマンドは **運用者／デプロイ向け**（modules 書き込み＝ホスト信頼境界）。テナント存在確認や容量上限は未実装。SaaS 向け強化は別途。
- Git / S3 / OCI からの取得 CLI は未対応（プロセスグローバル Source 設定とは別）。

zip レイアウトは [module-zip-layout 仕様](../specifications/actions/module-zip-layout.md) を参照。

## 次に読むもの

- Engine 単体: [engine-standalone-guide.md](engine-standalone-guide.md)
- Module 署名: [action-module-signing.md](action-module-signing.md)
