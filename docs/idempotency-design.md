# 冪等キー設計（Idempotency / command_dedup）

Version: 1.0  
関連: `docs/scheme.v2.md`, `docs/data-integration-contract.md`, タスク 0.4

---

## 1. 概要

Command API（POST /cancel 等）は `X-Idempotency-Key` により冪等を保証する。  
本ドキュメントは、現行の **idempotency_keys** から **command_dedup** への移行方針と、`command_fingerprint` の算出方法を定める。

---

## 2. 現行方式: idempotency_keys

### 2.1 スキーマ

- **テーブル**: `idempotency_keys`
- **主キー**: `(idempotency_key, endpoint)`
- **カラム**:
  - `idempotency_key` … クライアント指定の冪等キー（ヘッダ `X-Idempotency-Key`）
  - `endpoint` … リクエストの「エンドポイント識別子」（同一キーでもエンドポイントが違えば別扱い）
  - `request_hash` … リクエストボディのハッシュ（同一キーでボディが違うと 409）
  - `response_status`, `response_body`, `created_at`

### 2.2 endpoint の算出

- `endpointKey(req)` で決定（`presentation/http/middleware.ts`）
- 形式: `{method} {baseUrl}{path}`。パス内の UUID/数値等は `:id` に正規化
- 例: `POST /executions/:id/cancel` → 実行ごとに同じ endpoint、キーが同じなら同一コマンドとして扱う

### 2.3 request_hash の算出

- **算出方法**: リクエストボディの正規化 JSON の SHA-256（hex）
- **実装**: `IdempotencyService.hashRequest(body)`  
  `SHA256(JSON.stringify(body ?? null))`
- **用途**: 同一 `(idempotency_key, endpoint)` でボディが異なる再送は **409 Conflict** とする

---

## 3. 新方式: command_dedup

### 3.1 スキーマ（scheme.v2）

- **テーブル**: `command_dedup`
- **主キー**: `(tenant_id, idempotency_key)`
- **カラム**:
  - `tenant_id` … テナントID（ヘッダまたはデフォルト `"default"`）
  - `idempotency_key` … 同上
  - `command_fingerprint` … コマンド内容のハッシュ（後述）
  - `response_status`, `response_body`, `created_at`

マイグレーション: `services/core-api/sql/003_add_command_dedup.sql`

### 3.2 キー設計の変更点

| 項目       | idempotency_keys              | command_dedup                    |
|------------|-------------------------------|----------------------------------|
| キー範囲   | (idempotency_key, endpoint)   | (tenant_id, idempotency_key)     |
| スコープ   | エンドポイント単位             | テナント単位                     |
| ボディ識別 | request_hash                  | command_fingerprint              |

- **endpoint をやめる理由**: 冪等は「同じテナント・同じクライアント指定キー」で一意にすれば足りる。エンドポイントごとに分けると、同じ論理コマンドがパス違いで重複登録される余地がある。
- **tenant_id を入れる理由**: マルチテナントで、テナント間で idempotency_key が被っても衝突しないようにする。

### 3.3 command_fingerprint の算出方法

- **意味**: その冪等キーで「どのコマンド（リクエスト内容）」だったかを表す値。同一キーで内容が違う再送は 409 とする。
- **算出方法**（現行の request_hash と同一）:
  1. リクエストボディを正規化する。未送信の場合は `null` とする。
  2. `JSON.stringify(body ?? null)` の UTF-8 バイト列に対して SHA-256 を計算する。
  3. そのハッシュを 16 進文字列（lowercase hex）で表したものを `command_fingerprint` とする。

- **実装例**（現行 `IdempotencyService.hashRequest` を流用可）:
  ```ts
  const commandFingerprint = crypto
    .createHash("sha256")
    .update(JSON.stringify(body ?? null))
    .digest("hex");
  ```

- **注意**:
  - キー順序に依存しないようにする必要がある場合は、ボディを正規化（キーソート等）してから `JSON.stringify` する。現行は `JSON.stringify` のみで、実装と契約を合わせる。

---

## 4. 移行方針（idempotency_keys → command_dedup）

### 4.1 手順

1. **command_dedup テーブルを用意する**  
   タスク 0.3 のマイグレーション `003_add_command_dedup.sql` を適用済みであること。

2. **Core-API の冪等処理を command_dedup に切り替える**（タスク 2.4 で実施）
   - 冪等の「読む・書く」を `idempotency_keys` ではなく `command_dedup` に変更する。
   - キーは `(tenant_id, idempotency_key)`。`tenant_id` はリクエストヘッダ（例: `X-Tenant-Id`）または未指定時は `"default"`。
   - 新規リクエストは `command_dedup` にのみ記録する。
   - 既存の `idempotency_keys` は参照しない（過去分は移行しない想定でよい）。

3. **同一キーでボディが違う場合**
   - 既存と同様に **409 Conflict** を返す。
   - レスポンスには `error.code`（例: `IDEMPOTENCY_KEY_REUSED`）と、`command_fingerprint` が一致しない旨を分かるメッセージを含めるとよい。

4. **idempotency_keys の廃止**（タスク 2.9）
   - command_dedup への切り替えが完了し、運用で問題ないことを確認したのち、`idempotency_keys` を参照するコードを削除する。
   - テーブル削除用マイグレーションは任意（履歴を残す場合は残す）。

### 4.2 データ移行

- **推奨**: 既存の `idempotency_keys` のデータを `command_dedup` にバックフィルしない。
- 理由: スキーマが異なり（endpoint vs tenant_id）、対応関係が一意でない。新方式適用後のリクエストから command_dedup だけで運用する。

### 4.3 互換性

- クライアントは従来どおり `X-Idempotency-Key` を付与する。API の振る舞い（202 の重複時は前回レスポンスを返す、ボディ違いで 409）は維持する。
- 新たに `tenant_id` をヘッダで渡す場合のみ、テナント単位の冪等になる。未指定時は `"default"` で従来に近い単一テナント動作とする。

---

## 5. まとめ

- **現行**: `(idempotency_key, endpoint)` + `request_hash`（ボディの SHA-256 hex）
- **新方式**: `(tenant_id, idempotency_key)` + `command_fingerprint`（同上、名前だけ変更）
- **移行**: command_dedup を新規のみに使い、idempotency_keys は参照廃止後に削除。
- **fingerprint**: `SHA256(JSON.stringify(body ?? null)).digest("hex")` で算出し、同一キーで値が違えば 409 とする。
