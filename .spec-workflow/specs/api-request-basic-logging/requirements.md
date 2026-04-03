# Requirements: API リクエスト基本ログ（STV-403 / LOG-1）

## Introduction

Statevia Core-API（ASP.NET Core）に、**各 HTTP リクエストの開始・正常終了・未処理例外**を `ILogger` で追跡可能にする。運用・開発時に `traceId` で開始と完了を相関し、レイテンシとステータスを把握できるようにする。

**紐づくチケット**: `STV-403`（`v2-ticket-backlog.md`）、`LOG-1`（`v2-logging-v1-tasks.md`）。

## Alignment with Product Vision

v2 では Core-API が契約の正と運用の入口である。リクエスト単位の可観測性は、インシデント調査とパフォーマンス確認の前提であり、「定義駆動・イベントソース」の運用を支える。

## Requirements

### Requirement 1 — リクエスト開始ログ

**User Story:** As a **運用・開発者**, I want **各リクエスト受信時に構造化された開始ログ**を得たい, so that **どのテナントがどのパスにアクセスしたかを追える**。

#### Acceptance Criteria

1. WHEN **Core-API が HTTP リクエストを受け付ける** THEN **システムは `ILogger` に Info ログを出力する**。
2. WHEN **上記ログが出力される** THEN **少なくとも次の文脈が含まれる**: `traceId`, `method`, `path`, `tenantId`（`X-Tenant-Id` またはプロキシ既定の解決結果）。
3. IF **`v2-logging-v1-tasks.md` の API 表に従う** THEN **開始ログに `userAgent` を含めてよい**（未送信時は空または省略を明文化）。

### Requirement 2 — リクエスト完了ログ

**User Story:** As a **運用・開発者**, I want **レスポンス送出前に完了ログ**を得たい, so that **ステータスコードと処理時間を traceId で相関できる**。

#### Acceptance Criteria

1. WHEN **リクエスト処理が完了しレスポンスが返る** THEN **システムは `ILogger` に Info ログを出力する**。
2. WHEN **完了ログが出力される** THEN **少なくとも次が含まれる**: `traceId`, `statusCode`, `elapsedMs`。
3. IF **ログ項目表（Logging v1）をこの段階まで拡張する** THEN **`responseSize`（バイトまたは同義）を含めてよい**（未計測時は 0 または省略を明文化）。

### Requirement 3 — 例外・5xx 経路のログ

**User Story:** As a **運用・開発者**, I want **未処理例外や 5xx 応答を Error で記録**したい, so that **失敗リクエストを特定できる**。

#### Acceptance Criteria

1. WHEN **未処理例外がミドルウェアまたはホストで捕捉される** THEN **システムは `ILogger` に Error ログを出力する**。
2. WHEN **Error ログが出力される** THEN **少なくとも次が含まれる**: `traceId`, `errorType`, `message`。`stack` は **本番ではマスクまたは抑制可能**とし、開発環境では出力してよい（方針をコードまたは設定で明文化）。
3. WHEN **API が 5xx を返す** THEN **完了ログ（Requirement 2）とあわせて追跡可能である**（同一 `traceId`）。

### Requirement 4 — traceId の一貫性

**User Story:** As a **運用・開発者**, I want **1 リクエストのライフサイクルで同一 traceId**を使いたい, so that **開始・完了・例外をログ基盤で結合できる**。

#### Acceptance Criteria

1. WHEN **クライアントが `traceparent` または（採用する場合）`X-Request-Id` / `X-Trace-Id` を送る** THEN **システムはそれをログの `traceId` に利用できる**（優先順位を design で定義）。
2. IF **受信ヘッダに追跡 ID がない** THEN **システムはリクエスト単位で一意な `traceId` を生成する**。
3. WHEN **同一リクエストの開始・完了・例外ログを照合する** THEN **同一の `traceId` 文字列で相関できる**。

### Requirement 5 — 検証とドキュメント

**User Story:** As a **リポジトリ保守者**, I want **テストまたは手順でログが検証可能**であること, so that **回帰でログが消えない**。

#### Acceptance Criteria

1. WHEN **実装がマージ対象である** THEN **単体テストまたは統合テストで、主要経路のログ呼び出しまたは出力が検証される**（最低 1 ケース可）。
2. WHEN **開発者がローカルで確認する** THEN **`AGENTS.md` 相当または `docs/` に、ログの見方・期待フィールドの短い手順が書かれている**（新規ファイルは最小限；既存への追記でよい）。

## Non-Functional Requirements

### Code Architecture and Modularity

- **単一責任**: リクエストロギングは専用ミドルウェアまたはフィルターに集約し、Controller に重複ロギングをばらまかない。
- **ASP.NET 慣習**: `ILogger<T>` / `LoggerMessage` source generator の利用を優先検討。
- **レイヤー**: HTTP 入出力に紐づくため Hosting 層に近い場所に置く（Controller ビジネスロジックに混在しない）。

### Performance

- ログ出力はリクエストCritical Pathでブロックしないこと（async ログが標準であればそのまま利用）。
- `elapsedMs` 計測のオーバーヘッドはマイクロ秒級に抑える。

### Security

- `path` に機密クエリが含まれる場合は **design でマスキング方針**を定める（STV-408 本格対応前でも、明らかな秘密はログに出さない）。

### Reliability

- ログ失敗でリクエストが 500 にならないこと。

## Out of Scope（本 spec の design / tasks に含めない）

- Engine 内部の workflow/state ログ（`STV-404` / LOG-2）。
- OpenTelemetry 全面導入、ELK 連携（`v2-logging-v1-tasks.md` 非目標）。
- ログキー名の全スタック統一（`STV-407` で扱う。本 spec では既存命名と衝突しない範囲で導入）。

## References

- `.workspace-docs/50_tasks/10_in-progress/v2-ticket-backlog.md` — STV-403
- `.workspace-docs/50_tasks/10_in-progress/v2-logging-v1-tasks.md` — LOG-1、API ログ項目表
- `AGENTS.md` — Core-API レイヤー方針
