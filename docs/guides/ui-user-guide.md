# UI（Studio）利用ガイド

| 項目 | 値 |
| --- | --- |
| 種別 | Guide |
| Version | 1.0 |
| 更新日 | 2026-07-07 |
| 関連 | [../specifications/ui/visual.md](../specifications/ui/visual.md) |

---

**Statevia Studio**（`ui/studio/`）は Next.js の Web ダッシュボードです。Core-API へはブラウザから直接呼ばず、**同一オリジンのプロキシ**（`/api/core/*`）経由でアクセスします。

## 起動

Core-API と PostgreSQL が起動している状態で:

```bash
cd ui/studio
CORE_API_INTERNAL_BASE="http://localhost:8080" npm run dev
```

ブラウザで `http://localhost:3000` を開きます。Docker Compose 利用時は [guides/operations-docker.md](guides/operations-docker.md) を参照。

## サインイン

`/login` からテナントキー・メール・パスワードでサインインします。初回管理者の作成は [operations-tenant-bootstrap.md](operations-tenant-bootstrap.md)。環境変数とテナント設定は [ui-auth-tenant-config.md](ui-auth-tenant-config.md)。

## 主な画面（概要）

| 画面 | 目的 |
| --- | --- |
| 定義一覧 / エディタ | ワークフロー定義の閲覧・編集（nodes 形式） |
| 実行一覧 | テナント内の実行インスタンス |
| 実行詳細 / グラフ | 状態・エッジ・進行の可視化 |

グラフのマージ規則・ノード表現の Normative 契約は [visual 仕様](../specifications/ui/visual.md)。リアルタイム更新は SSE（[push-api 仕様](../specifications/ui/push-api.md)）。

## 開発時の品質チェック

```bash
npm run lint
npm run typecheck
npm run test:run
```

## 次に読むもの

- HTTP 例: [http-request-examples.md](http-request-examples.md)
- UI 可視化仕様: [../specifications/ui/visual.md](../specifications/ui/visual.md)
