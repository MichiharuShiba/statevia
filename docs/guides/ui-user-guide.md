# UI（Studio）利用ガイド

| 項目 | 値 |
| --- | --- |
| 種別 | Guide |
| Version | 1.7 |
| 更新日 | 2026-09-01 |
| 関連 | [../specifications/ui/visual.md](../specifications/ui/visual.md)、[../specifications/api-http.md](../specifications/api-http.md) §2.1.2–2.2 |

**Version 1.7（2026-09-01）**: 品質チェックと内部構成をコントリビュータ向けと明示。SSE は Principal 必須。

**Version 1.6（2026-08-19）**: 新規・更新パスワードは 8〜128 文字・空白なし（記号可。大文字小文字の混在は不要）。

**Version 1.5（2026-08-19）**: ヘッダーからログアウトできる（Studio のセッション Cookie を削除して `/login` へ戻る）。

**Version 1.4（2026-08-19）**: `/account` で本人パスワード更新。ユーザー管理から管理者が対象ユーザーのパスワードを更新できる。

**Version 1.3（2026-08-17）**: サインイン識別子をメールからユーザー名へ変更。

**Version 1.2（2026-07-26）**: Studio 内部の feature-first 構成へのリンクを追加。利用者向け操作と HTTP 契約は変更なし。

---

**Statevia Studio**（`ui/studio/`）は Next.js の Web ダッシュボードです。Service API へはブラウザから直接呼ばず、**同一オリジンのプロキシ**（`/api/core/*`）経由でアクセスします。

## 起動

Service API と PostgreSQL が起動している状態で:

```bash
cd ui/studio
SERVICE_API_INTERNAL_BASE="http://localhost:8080" npm run dev
```

ブラウザで `http://localhost:3000` を開きます。Docker Compose 利用時は [operations-docker.md](operations-docker.md) を参照。

## サインイン

`/login` からテナントキー・ユーザー名・パスワードでサインインします。初回管理者の作成は [operations-tenant-bootstrap.md](operations-tenant-bootstrap.md)。環境変数とテナント設定は [ui-auth-tenant-config.md](ui-auth-tenant-config.md)。

ログイン後はヘッダーの「ログアウト」で Studio のセッション Cookie を消し、`/login` へ戻ります（ログイン画面では出しません）。Service API の JWT をサーバー側で即時失効はしません。ブラウザからセッションが無くなれば Studio は再ログインが必要です。

パスワードを忘れた場合のメール再設定は提供しません。管理者がユーザー管理から上書きするか、本人が `/account` で現行パスワードを確認して更新します。更新後も既存セッション（JWT）は期限まで有効です。

## 主な画面（概要）

| 画面 | 目的 |
| --- | --- |
| アカウント (`/account`) | 現行パスワード確認付きで自分のパスワードを更新 |
| ユーザー管理 | テナント管理者向け。作成・有効化/無効化・パスワード上書き |
| 定義一覧 / エディタ | ワークフロー定義の閲覧・編集（nodes 形式） |
| 実行一覧 | テナント内の実行インスタンス |
| 実行詳細 / グラフ | 状態・エッジ・進行の可視化 |

### パスワード更新

| 操作 | 画面 | 備考 |
| --- | --- | --- |
| 本人更新 | ヘッダー「アカウント」→ `/account` | 現行・新規・確認。確認不一致では送信しない。管理者以外も利用可。新規は 8〜128 文字・空白なし（記号可） |
| 管理者更新 | ユーザー管理の各行 | ダイアログで新規と確認。現行パスワードは不要。新規は 8〜128 文字・空白なし（記号可） |

HTTP 契約の正本は [api-http.md](../specifications/api-http.md) §4.1.1 / §4.1.3。

### 定義 catalog の論理削除・復元

Studio の定義一覧・詳細から Service API の catalog ライフサイクルを操作できる。

| 操作 | 画面 | 備考 |
| --- | --- | --- |
| 論理削除 | 一覧（active 行）・詳細 | インライン二段階確認のあと `DELETE /v1/definitions/{id}` |
| 削除済みを含む一覧 | 一覧の「削除済みを含む」 | URL `includeDeleted=true`。行に `deletedAt` と削除バッジ |
| 復元 | 一覧の削除済み行のみ | 単体 GET は 404（operational invisibility）のため詳細からは復元しない |

HTTP 契約の正本は [api-http.md](../specifications/api-http.md) §2.1.2–2.2。

グラフのマージ規則・ノード表現の Normative 契約は [visual 仕様](../specifications/ui/visual.md)。リアルタイム更新は SSE（[push-api 仕様](../specifications/ui/push-api.md)）。SSE も Principal 必須（未認証は 401）。Studio はプロキシ経由でセッション Cookie を付ける。

## コントリビュータ向け

Studio の内部モジュール境界（薄い `app/` + `features/` + `shared/`）は [ui-studio-structure.md](../architecture/ui-studio-structure.md) を正本とする。画面追加や import 規約はそちらを参照する（本ガイドの利用者向け操作・HTTP 契約は変えない）。

品質チェック:

```bash
npm run lint
npm run typecheck
npm run test:run
```

## 次に読むもの

- HTTP 例: [http-request-examples.md](http-request-examples.md)
- UI 可視化仕様: [../specifications/ui/visual.md](../specifications/ui/visual.md)
