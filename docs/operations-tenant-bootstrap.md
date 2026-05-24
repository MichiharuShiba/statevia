# テナント・初回管理者ブートストラップ

- Version: 1.0.0
- 更新日: 2026-05-21
- 関連: `docs/runtime-security-boundary.md`, `docs/operations-docker.md`

---

## 前提

- **テナント発行 UI はスコープ外**。本手順は運用者・DB 管理者向け。
- マイグレーション適用時に **`tenant_key = default`** のテナントがシードされる（既存 `"default"` ヘッダ互換）。
- 追加テナントは SQL または将来の platform ツールで作成する。

## 1. マイグレーション適用

```bash
cd api
dotnet ef database update --project Statevia.Core.Api
```

## 2. テナント作成（追加テナント）

```sql
INSERT INTO tenants (id, tenant_key, display_name, lifecycle, created_at, updated_at)
VALUES (
  gen_random_uuid(),
  'acme-corp',           -- immutable。SDK/CLI に埋め込むキー
  'Acme Corporation',
  'Active',
  NOW(),
  NOW()
);
```

`tenant_key` は **作成後変更しない**（`docs/runtime-security-boundary.md` 参照）。

## 3. 初回テナント管理者（Principal 整合）

1. **Principal** を Tenant スコープで作成する。
2. **User** 行を同一テナントに作成し `is_tenant_admin = true` とする。
3. **user_principals** で 1:1 紐付けする。

パスワードハッシュは Core-API 内の `PasswordHasher` と同じアルゴリムを使う。開発環境では `POST /v1/auth/login` 成功後に JWT を取得して以降の API を呼ぶ。

### 推奨: ログイン API 経由で検証

```bash
curl -s -X POST http://localhost:8080/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"tenantKey":"default","email":"admin@example.com","password":"<plain-for-dev-only>"}'
```

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
