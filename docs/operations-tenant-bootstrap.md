# テナント・初回管理者ブートストラップ

- Version: 1.1.0
- 更新日: 2026-05-31
- 関連: `docs/runtime-security-boundary.md`, `docs/operations-docker.md`

---

## 前提

- **テナント発行 UI はスコープ外**。本手順は運用者・DB 管理者向け。
- マイグレーション適用時に **`tenant_key = default`** のテナントがシードされる（既存 `"default"` ヘッダ互換）。
- 追加テナントは SQL または将来の platform ツールで作成する。

## 1. マイグレーション適用

```bash
cd service/api
dotnet ef database update --project Statevia.Service.Api
```

## 2. テナント作成（追加テナント）

`tenant_key` は **作成後変更しない**（`docs/runtime-security-boundary.md` 参照）。キーは **小文字・数字・ハイフン・ドット**（先頭末尾のハイフン/ドット不可、最大 64 文字。例: `acme-corp`, `statevia.dev`）。

### 推奨: ブートストラップ CLI

**PowerShell（リポジトリルート）:**

```powershell
# 環境変数
$env:DATABASE_URL = "postgres://statevia:statevia@localhost:5432/statevia"
.\scripts\bootstrap-tenant.ps1 -TenantKey "acme-corp" -DisplayName "Acme Corporation"

# または引数で DB を指定
.\scripts\bootstrap-tenant.ps1 -TenantKey "acme-corp" `
  -DatabaseUrl "postgres://statevia:statevia@localhost:5432/statevia"

# Core API の appsettings を使う場合
.\scripts\bootstrap-tenant.ps1 -TenantKey "acme-corp" `
  -Config "service/api/Statevia.Service.Api/appsettings.json"
```

**dotnet のみ:**

```bash
cd service/api
dotnet run --project Statevia.Service.Api.Bootstrap -- \
  --database-url "postgres://statevia:statevia@localhost:5432/statevia" \
  create-tenant \
  --tenant-key acme-corp \
  --display-name "Acme Corporation"
```

| オプション（グローバル・コマンドの前） | 説明 |
| --- | --- |
| `--database-url` / `--connection-string` | PostgreSQL URL または Npgsql 形式（`DATABASE_URL` より優先） |
| `--config <path>` | 追加の JSON 設定（例: `service/api/Statevia.Service.Api/appsettings.json`） |

読み込み順（低→高）: カレントの `appsettings.json` → `--config` → 環境変数 → `--database-url`。

| オプション（create-tenant） | 説明 |
| --- | --- |
| `--tenant-key` | 外部キー（必須・immutable） |
| `--display-name` | 表示名（省略時は tenant-key） |
| `--skip-if-exists` | 同一 `tenant_key` があれば何もしない |

作成後は権限カタログ（`permission_definitions`）を自動投入する。続けて [§3](#3-初回テナント管理者principal-整合) で管理者を作成する。

### 参考: 手動 SQL

```sql
INSERT INTO tenants (tenant_id, tenant_key, display_name, lifecycle, created_at, updated_at)
VALUES (
  gen_random_uuid(),
  'acme-corp',
  'Acme Corporation',
  'Active',
  NOW(),
  NOW()
);
```

## 3. 初回テナント管理者（Principal 整合）

1. **Principal** を Tenant スコープで作成する。
2. **User** 行を同一テナントに作成し `is_tenant_admin = true` とする。
3. **user_principals** で 1:1 紐付けする。

パスワードハッシュは Core-API 内の `PasswordCredentialService`（ASP.NET Core `PasswordHasher`）と同じアルゴリズムを使う。

### 推奨: ブートストラップ CLI（手動 SQL の代替）

`tenant_key = default` のテナントは API 起動時にシードされる。追加テナントは [§2](#2-テナント作成追加テナント) のあと、同じ CLI で `--tenant-key` を指定する。

**PowerShell（リポジトリルート）:**

```powershell
$env:DATABASE_URL = "postgres://statevia:statevia@localhost:5432/statevia"
$env:STATEVIA_BOOTSTRAP_PASSWORD = "<plain-for-dev-only>"
.\scripts\bootstrap-tenant-admin.ps1 -Email "admin@example.com"
```

**dotnet のみ:**

```bash
cd service/api
export DATABASE_URL="postgres://statevia:statevia@localhost:5432/statevia"
export STATEVIA_BOOTSTRAP_PASSWORD="<plain-for-dev-only>"
dotnet run --project Statevia.Service.Api.Bootstrap -- \
  create-admin \
  --tenant-key default \
  --email admin@example.com \
  --password "$STATEVIA_BOOTSTRAP_PASSWORD"
```

| オプション | 説明 |
| --- | --- |
| `--tenant-key` | 外部キー（既定 `default`） |
| `--email` | 管理者メール（必須） |
| `--password` | 平文（省略時は `STATEVIA_BOOTSTRAP_PASSWORD`） |
| `--display-name` | Principal 表示名（省略時は email） |
| `--skip-if-exists` | ログイン可能な同一メールが既にいれば何もしない |

成功時は `tenantId` / `userId` / `principalId` を標準出力する。パスワードはログに出さないこと。

### 推奨: ログイン API / UI 経由で検証

```bash
curl -s -X POST http://localhost:8080/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"tenantKey":"default","email":"admin@example.com","password":"<plain-for-dev-only>"}'
```

UI では `/login` から同じ資格情報でサインインできる（`ui/studio`）。

## 4. API キー（CI / サーバー間）

- **`api_keys`** 行を作成する。平文キーは **一度だけ** 利用者に渡し、DB には **prefix + hash** のみ保存する。
- `allowed_scopes` は JSON 配列。有効権限は **グループ展開 ∩ allowed_scopes**（交差のみ）。
- 初版のキー検証ミドルウェアは後続タスク。スキーマとハッシュ方針は本リリースで固定する。

## 5. ライフサイクル変更

| 操作 | 手順 |
| --- | --- |
| 一時停止 | `UPDATE tenants SET lifecycle = 'Suspended', updated_at = NOW() WHERE tenant_key = '...'` |
| アーカイブ | `Active` または `Suspended` から `Archived` のみ許可 |
| 復帰 | `Suspended` → `Active` のみ。`Archived` からの復帰は不可 |

停止中テナントでの API 呼び出しは **403**（`TENANT_SUSPENDED` / `TENANT_ARCHIVED`）。

## 6. ロールバック

- 本フェーズのマイグレーションは **`Down` 可能**（security テーブル drop + 既存テーブル無変更）。
- 本番では Down の代わりに **手動で security テーブルを truncate/drop** する手順を runbook に残す。
