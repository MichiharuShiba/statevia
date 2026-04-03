# U2: event_store のスキーマ議論

event_store は**イベントソース専用**テーブル。リプレイ・監査・projection 更新の元データとなる。  
U1 の決定に基づき、**seq は API が INSERT 時に付与**する。  
このドキュメントでは **event_store の列定義**（EventEnvelope のどのフィールドを独立した列にするか、どれを JSON に含めるか）を議論する。

---

## 1. 参照: EventEnvelope（docs/core-events-spec）

| フィールド    | 型               | 必須 | 説明                                                               |
| ------------- | ---------------- | ---- | ------------------------------------------------------------------ |
| eventId       | string (UUID)    | ✓    | イベントの一意識別子。重複排除・べき等に利用。                     |
| executionId   | string           | ✓    | 実行（ワークフロー）の ID。本計画では **workflow_id** と同一。     |
| type          | string           | ✓    | 24 種の固定値（EXECUTION_CREATED, NODE_SUCCEEDED 等）。            |
| occurredAt    | string (RFC3339) | ✓    | イベントの「発生時刻」。                                           |
| actor         | object           | ✓    | kind: "system" \| "user" \| "scheduler" \| "external", id?: string |
| correlationId | string           | 任意 | 関連リクエストの追跡用。                                           |
| causationId   | string           | 任意 | 直前イベントの eventId など、因果関係。                            |
| schemaVersion | number           | ✓    | 1（将来のスキーマ進化用）。                                        |
| payload       | object           | ✓    | type ごとのペイロード。                                            |

**注意**: 本計画では集約 ID を **workflow_id** と呼ぶ。core-events-spec の executionId は workflow_id に対応する。

---

## 2. event_store の利用パターン

| 用途                 | アクセスパターン                            | 必要な列・検索                                                                   |
| -------------------- | ------------------------------------------- | -------------------------------------------------------------------------------- |
| **リプレイ**         | workflow 単位で seq 順に読み出す            | workflow_id, seq（必須）。ORDER BY seq で取得。                                  |
| **重複排除・べき等** | 同一 eventId の再挿入を防ぐ                 | event_id（UUID）の UNIQUE 制約。                                                 |
| **監査・トレース**   | 時刻範囲、actor、correlation で検索         | created*at / occurred_at, actor*\*, correlation_id 等があると SQL で絞りやすい。 |
| **projection 更新**  | バッチで読み出したイベントを reducer に渡す | 行を EventEnvelope に復元できればよい（列でも JSON でも可）。                    |

---

## 3. 選択肢

### 3.1 案 A: 正規化列（メタデータをすべて列に持つ）

#### 案 A の列イメージ

| column         | type        | 説明                                                  |
| -------------- | ----------- | ----------------------------------------------------- |
| event_id       | uuid        | EventEnvelope.eventId。UNIQUE 制約で重複排除。        |
| workflow_id    | uuid        | 集約 ID（executionId 相当）。                         |
| seq            | bigint      | workflow_id 単位の単調増加。API が INSERT 時に付与。  |
| type           | varchar     | EXECUTION_CREATED 等。                                |
| occurred_at    | timestamptz | イベント発生時刻（RFC3339 を DB では timestamp で）。 |
| actor_kind     | varchar     | system / user / scheduler / external                  |
| actor_id       | varchar     | 任意                                                  |
| correlation_id | varchar     | 任意                                                  |
| causation_id   | uuid        | 任意（直前イベントの event_id）。                     |
| schema_version | int         | 1。将来のスキーマ進化用。                             |
| payload_json   | jsonb       | EventEnvelope.payload のみ。                          |
| created_at     | timestamptz | 行を DB に記録した時刻（任意だが監査で有用）。        |

#### 案 A のメリット

- **監査・検索が容易**: actor_kind, correlation_id, occurred_at で SQL の WHERE / インデックスが使える。
- **リプレイは単純**: `SELECT * FROM event_store WHERE workflow_id = ? ORDER BY seq` で取得し、列を EventEnvelope に組み立てればよい。
- **型が明確**: UUID / timestamp を DB 側で保証できる。

#### 案 A のデメリット

- **列数が多い**: EventEnvelope の拡張（新しいメタデータ）のたびにマイグレーションが必要。
- **スキーマ進化**: schema_version を上げて payload の解釈を変えることはできるが、列の追加・変更はマイグレーションコストがかかる。

---

### 3.2 案 B: 最小列 + エンベロープ全体を JSON に格納

#### 案 B の列イメージ

| column        | type   | 説明                                                                             |
| ------------- | ------ | -------------------------------------------------------------------------------- |
| workflow_id   | uuid   | 集約 ID。                                                                        |
| seq           | bigint | workflow_id 単位の単調増加。API が付与。                                         |
| envelope_json | jsonb  | EventEnvelope 全体（eventId, type, occurredAt, actor, payload 等をすべて含む）。 |

#### 案 B のメリット

- **スキーマ変更に強い**: EventEnvelope にフィールドが増えても、JSON の拡張だけで済む。マイグレーションが少ない。
- **列が少ない**: 3 列で済む。
- **リプレイ**: `WHERE workflow_id = ? ORDER BY seq` で取得し、envelope_json をそのまま EventEnvelope にデシリアライズすればよい。

#### 案 B のデメリット

- **重複排除**: eventId で一意にしたい場合、**event_id を列に持たない**と UNIQUE 制約が張れない。envelope_json 内の eventId に UNIQUE を張るには PostgreSQL の式インデックス（例: `(envelope_json->>'eventId')`）が必要。
- **監査・検索**: actor や correlation_id で絞るには JSON 演算子（`envelope_json->'actor'->>'kind'` 等）を使う必要があり、インデックスも式インデックスになる。
- **型**: JSON 内の日付・UUID は DB 側では文字列のまま。検索・比較はアプリまたは式インデックスに依存。

---

### 3.3 案 C: ハイブリッド（リプレイ・一意に必要な列 + メタは JSON）

#### 案 C の列イメージ

| column            | type        | 説明                                                                                                                                                    |
| ----------------- | ----------- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| event_id          | uuid        | EventEnvelope.eventId。UNIQUE 制約。                                                                                                                    |
| workflow_id       | uuid        | 集約 ID。                                                                                                                                               |
| seq               | bigint      | workflow_id 単位の単調増加。API が付与。                                                                                                                |
| type              | varchar     | イベント種別。リプレイ時のフィルタやインデックスに利用可能。                                                                                            |
| occurred_at       | timestamptz | 発生時刻。監査・時間範囲検索用。                                                                                                                        |
| meta_payload_json | jsonb       | actor, correlation_id, causation_id, schema_version, payload をまとめた JSON。または envelope の「type と payload 以外」を meta、payload を分けても可。 |

**リプレイ時**: event_id, workflow_id, seq, type, occurred_at と meta_payload_json から EventEnvelope を組み立てる。

#### 案 C のメリット

- リプレイと一意性に必要な **event_id, workflow_id, seq** と、検索に使いやすい **type, occurred_at** は列。
- その他のメタデータ（actor, correlation_id, causation_id, schema_version）と payload は JSON で柔軟に。
- 案 A より列数が少なく、案 B より監査・検索と UNIQUE がしやすい。

#### 案 C のデメリット

- 「どのフィールドを列にするか」の境界を決める必要がある。
- meta_payload_json の構造（flat か nested か）を決める必要がある。

---

## 4. その他の検討事項

### 4.1 created_at と occurred_at

- **occurred_at**: イベントが「論理的に発生した時刻」。Engine が付与。リプレイの意味づけや監査で重要。
- **created_at**: 行が DB に挿入された時刻。監査（「いつ記録されたか」）やデバッグに有用。
- **推奨**: 両方持つ。event_store は **occurred_at 必須**。**created_at** は任意だがあるとよい。

### 4.2 workflow_events（監査用）との対応

- U1 の決定で、**event_store と workflow_events は同一トランザクションで INSERT** する。
- workflow_events の列は現行 db-schema.md では `id, workflow_id, seq, type, payload_json, created_at`。
- **選択**:
  - **A) workflow_events は「監査用の簡易コピー」**: event_store と列を揃えず、監査に必要な最小限（workflow_id, seq, type, payload_json, created_at）だけ持つ。詳細は event_store を参照。
  - **B) workflow_events も EventEnvelope 相当を持つ**: event_store と同様の列または JSON を持たせ、監査クエリを workflow_events だけで完結させる。
- 同一トランザクションで 2 テーブルに書くなら、**event_store を正**とし、workflow_events は event_store から必要な列だけコピーする形（A）にすると、スキーマの二重管理を避けられる。

### 4.3 インデックス・制約

- **必須**: `(workflow_id, seq)` の UNIQUE 制約（およびリプレイ用インデックス）。
- **重複排除**: `event_id` の UNIQUE 制約（案 A / 案 C）。案 B の場合は `(envelope_json->>'eventId')` の UNIQUE 式インデックスを検討。
- **監査**: 時間範囲検索が多いなら `occurred_at` または `created_at` のインデックスを検討。

### 4.4 型（PostgreSQL）

- **workflow_id / event_id**: `uuid` 型で統一すると比較・インデックスが効く。
- **seq**: 1 workflow あたりのイベント数が非常に多くなる想定なら `bigint`。
- **payload / envelope / meta**: `jsonb` にすると GIN インデックスや JSON 演算子が使える。

---

## 5. まとめと選択の軸

| 軸           | 案 A（正規化）                   | 案 B（envelope_json） | 案 C（ハイブリッド）        |
| ------------ | -------------------------------- | --------------------- | --------------------------- |
| 監査・検索   | 強い（列で絞り込み）             | 弱い（JSON 演算子）   | 中（主要な列 + JSON）       |
| スキーマ進化 | 弱い（列追加のマイグレーション） | 強い（JSON の拡張）   | 中（列は固定、JSON は拡張） |
| リプレイ     | 単純                             | 単純                  | 単純                        |
| 重複排除     | event_id UNIQUE が自然           | 式インデックスが必要  | event_id UNIQUE が自然      |
| 列数         | 多い                             | 少ない                | 中程度                      |

### 推奨の方向性（議論用）

- **監査・トレースを SQL でよく使う**なら **案 A** または **案 C**。
- **まずはシンプルに始め、将来スキーマを変えにくいなら列を増やしたくない**なら **案 B**（その場合、event_id の一意性は式インデックスまたはアプリ側で担保）。
- **バランス**を取るなら **案 C**（event_id, workflow_id, seq, type, occurred_at を列、それ以外を JSON）。

---

## 6. 決定事項・オープンな論点（記入用）

### 6.1 決定事項

| 事項                 | 決定内容                                                                                          |
| -------------------- | ------------------------------------------------------------------------------------------------- |
| スキーマ方針         | **案 A（正規化）**。EventEnvelope のメタデータを列に持つ。                                        |
| created_at           | **持つ**。occurred_at（発生時刻）と意味が異なるため、記録時刻として created_at を設ける。         |
| workflow_events の列 | **最小限（監査専用）**。event_store と揃えず、監査に必要な列だけ持つ。詳細は event_store を参照。 |

### 6.2 オープンな論点

- 特になし（上記で決定済み）。

以上を、U2 event_store スキーマの議論のたたき台とする。
