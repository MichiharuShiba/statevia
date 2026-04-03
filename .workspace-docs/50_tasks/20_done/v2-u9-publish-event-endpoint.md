# U9: PublishEvent(eventName) のエンドポイント議論

**前提**（modification-plan / open-decisions 3.3）: Engine には **PublishEvent(workflowId, eventName)** と **PublishEvent(eventName)** の両方がある。前者は「特定 workflow にイベントを送る」、後者は「そのエンジン内の全ワークフローにブロードキャスト」する。API は両方に対応する。

- **workflow 単位**: `POST /v1/workflows/{id}/events`（body: `{ "name": "Approve" }`）→ Engine.PublishEvent(workflowId, eventName)。計画に明示済み。
- **全ワークフロー向け**: Engine.PublishEvent(eventName) に対応する **REST のパス・body・レスポンス**が計画に明示されていない。

本ドキュメントでは **全ワークフロー向け PublishEvent(eventName) の REST 表現** を議論する。

---

## 1. 論点

- **パス**: どの URL で「イベント名だけを送り、全 Running ワークフローにブロードキャストする」かを決める。
- **リクエスト body**: イベント名のみか、オプションでフィルタ（例: definitionId）を持つか。
- **レスポンス**: 200/202 の body に何を返すか（受信した workflow 数、影響した workflow の id 一覧、など）。または body なしでよいか。

---

## 2. パスの選択肢

| 案    | パス                        | 説明                                                            | メリット                               | デメリット                                                               |
| ----- | --------------------------- | --------------------------------------------------------------- | -------------------------------------- | ------------------------------------------------------------------------ |
| **A** | `POST /v1/events`           | トップレベルに events を置く。body でイベント名を送る。         | 短い。「イベントを発行する」と直感的。 | /v1/workflows と並列で、リソース階層が増える。                           |
| **B** | `POST /v1/workflows/events` | workflows の下に「workflow に紐づかないイベント」を置く。       | workflows 配下で一貫。                 | 「workflow の events」と紛らわしい（{id}/events は特定 workflow 向け）。 |
| **C** | 提供しない                  | 全ワークフロー向けは REST で公開せず、workflow 単位のみとする。 | 実装・運用が単純。誤発火が減る。       | ブロードキャストが必要なユースケースで不便。                             |

**補足**: 現行 Engine の PublishEvent(eventName) は「そのエンジン内の全ワークフロー」に届ける。v2 では 1 プロセス 1 エンジン（シングルトン）なので、実質「全 Running ワークフロー」へのブロードキャスト。REST で公開する場合は、**案 A** が分かりやすい。

---

## 3. リクエスト body

- **最小**: `{ "name": "Approve" }` のみ。workflow 単位の `POST /v1/workflows/{id}/events` と同じ形に揃えると、クライアントの実装が共通化しやすい。
- **拡張（将来）**: `definitionId` をオプションで指定し「その定義の Running ワークフローのみに送る」などは、必要になったら追加する。U9 では **イベント名のみ** で十分とする。

---

## 4. レスポンス

- **HTTP ステータス**: ブロードキャストは同期的に Engine に渡すだけなので **200 OK** でよい。非同期にする場合は 202 Accepted もあり得るが、Phase 2 では 200 で十分。
- **body**: (1) **空** または (2) **影響した workflow 数・id 一覧** のいずれか。デバッグや監査には (2) があると便利だが、実装コストとレスポンス肥大化のトレードオフ。**最小では空 body または `{ "published": true }` 程度** とし、必要なら後から `affectedWorkflowIds` 等を追加する。

---

## 5. 推奨の方向性

- **パス**: **案 A** `POST /v1/events`。body は `{ "name": "<eventName>" }`。workflow 単位とフィールド名を揃える。
- **レスポンス**: 200 OK。body は **空** または `{ "published": true }`。Phase 2 では影響範囲の詳細は返さない。
- **案 C（提供しない）** を選ぶ場合: 全ワークフロー向けは REST で公開せず、必要なら「複数 workflow に順に POST /v1/workflows/{id}/events を呼ぶ」形でクライアントが対応する。Engine の PublishEvent(eventName) は API からは使わない。

---

## 6. 決定事項・オープンな論点（記入用）

### 6.1 決定事項

| 事項                     | 決定内容                                                                                                                         |
| ------------------------ | -------------------------------------------------------------------------------------------------------------------------------- |
| 全ワークフロー向けのパス | **POST /v1/events** を用意する（決定）                                                                                           |
| リクエスト body          | **イベント名のみ**。`{ "name": "<eventName>" }`。workflow 単位と同一形式（決定）                                                 |
| レスポンス               | **200 OK**。body は空または `{ "published": true }`。Phase 2 では影響 id 一覧は返さない（決定）                                  |

### 6.2 オープンな論点

- 将来、`definitionId` でフィルタした「その定義の Running のみに送る」を入れるか。
- レスポンスに `affectedCount` や `affectedWorkflowIds` を返すか（監査・デバッグ用）。Phase 2 では見送りでよい。

以上を、U9 PublishEvent(eventName) のエンドポイントの議論とする。
