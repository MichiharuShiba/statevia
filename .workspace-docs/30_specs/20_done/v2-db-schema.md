# Database Schema

---

## event_store（イベントソース専用・案 A 正規化）

EventEnvelope を正規化して格納。リプレイは `WHERE workflow_id = ? ORDER BY seq`。seq は API が INSERT 時に付与。

| column         | type        | 説明                                                |
| -------------- | ----------- | --------------------------------------------------- |
| event_id       | uuid        | EventEnvelope.eventId。UNIQUE。重複排除・べき等用。 |
| workflow_id    | uuid        | 集約 ID（executionId 相当）。                       |
| seq            | bigint      | workflow_id 単位の単調増加。API が付与。            |
| type           | varchar     | EXECUTION_CREATED, NODE_SUCCEEDED 等。              |
| occurred_at    | timestamptz | イベント発生時刻。                                  |
| actor_kind     | varchar     | system / user / scheduler / external                |
| actor_id       | varchar     | 任意                                                |
| correlation_id | varchar     | 任意                                                |
| causation_id   | uuid        | 任意（直前イベントの event_id）。                   |
| schema_version | int         | 1。将来のスキーマ進化用。                           |
| payload_json   | jsonb       | EventEnvelope.payload のみ。                        |
| created_at     | timestamptz | 行を DB に記録した時刻。                            |

制約: `(workflow_id, seq)` UNIQUE、`event_id` UNIQUE。

---

## display_ids（表示用 ID 専用・U3 決定）

definition / workflow の表示用 ID（英数字）と UUID の対応。1 テーブルで kind により区別。display_id はグローバルで一意（definition と workflow で共有しない）。**62 文字種（0-9, a-z, A-Z）・10 桁**、乱数生成・衝突時は再生成。

| column      | type        | 説明                                        |
| ----------- | ----------- | ------------------------------------------- |
| kind        | varchar     | `"definition"` または `"workflow"`。        |
| display_id  | varchar(10) | 表示用 ID（62 文字・10 桁）。UNIQUE。       |
| resource_id | uuid        | 対応する definition_id または workflow_id。 |
| created_at  | timestamptz | 作成時刻（任意）。                          |

制約: `(kind, resource_id)` PK、`display_id` UNIQUE。表示用 ID は乱数生成し、INSERT 時のキー違反で衝突を検出して再生成する（事前 SELECT は行わない）。

---

## workflow_definitions

PK および FK は **uuid** 型に統一（C9 決定）。**tenant_id** でマルチテナントのスコープを切る（2.5）。未指定時は `'default'`。

| column        | type        |
| ------------- | ----------- |
| definition_id | uuid        |
| tenant_id     | varchar(64) |
| name          | varchar     |
| source_yaml   | text        |
| compiled_json | text        |
| created_at    | timestamptz |

---

## workflows

PK および FK は **uuid** 型に統一。再起動失効は **restart_lost** フラグで管理（C9・C12 決定）。**tenant_id** でマルチテナントのスコープを切る（2.5）。未指定時は `'default'`。

| column           | type        |
| ---------------- | ----------- |
| workflow_id      | uuid        |
| tenant_id        | varchar(64) |
| definition_id    | uuid        |
| status           | varchar     |
| started_at       | timestamptz |
| updated_at       | timestamptz |
| cancel_requested | bool        |
| restart_lost     | bool        |

---

## workflow_events（監査専用・最小限）

event_store と同一トランザクションで INSERT。監査用の最小列のみ。詳細は event_store を参照。**workflow_event_id** はこの監査レコードの PK（event_store の event_id とは別）。workflow_id は **uuid** 型（C9 決定）。

| column           | type        |
| ---------------- | ----------- |
| workflow_event_id | uuid        |
| workflow_id      | uuid        |
| seq          | bigint      |
| type         | varchar     |
| payload_json | jsonb       |
| created_at   | timestamptz |

---

## execution_graph_snapshots

| column      | type        |
| ----------- | ----------- |
| workflow_id | uuid        |
| graph_json  | text        |
| updated_at  | timestamptz |

---

## command_dedup（コマンド冪等制御）

`X-Idempotency-Key` によるコマンドの冪等制御用テーブル。  
`method + path + idempotency_key` で一意となるキーを保持し、**初回リクエストのレスポンスを一定期間（デフォルト 24 時間）キャッシュ**する。

| column          | type        | 説明 |
| --------------- | ----------- | ---- |
| dedup_key       | varchar     | `METHOD SPATH:idempotency_key` 形式の正規化キー（例: `POST /v1/workflows:{key}`）。PK または UNIQUE。 |
| endpoint        | varchar     | 論理エンドポイント名（例: `POST /v1/workflows`）。運用・集計用。 |
| idempotency_key | varchar     | 受信した `X-Idempotency-Key` の値。 |
| request_hash    | varchar     | リクエストボディのハッシュ（同じ dedup_key でボディが異なる場合の検知に使用）。 |
| status_code     | int         | 初回リクエスト時に返却した HTTP ステータスコード。 |
| response_body   | jsonb       | 初回リクエスト時に返却したレスポンスボディ（JSON）。 |
| created_at      | timestamptz | レコード作成時刻。 |
| expires_at      | timestamptz | 有効期限。**デフォルト値は `created_at + interval '24 hours'`**。 |

制約:

- `dedup_key` に PK または UNIQUE 制約を付与する。
- `expires_at` 経過後のレコードはクリーンアップ対象（実装は任意のバッチ／ジョブとする）。
