# Requirements: ログ関連テスト（STV-409 / LOG-7）

## Introduction

`STV-403`～`STV-408` で導入・変更したログ経路について、**単体テスト**で回帰を検知できるようにする。失敗・Warning 経路を含め、重要な挙動が **意図せず消えない**ことを保証する。

**紐づくチケット**: `STV-409`（`v2-ticket-backlog.md`）、`LOG-7`（`v2-logging-v1-tasks.md`）。

**依存**: `STV-403`～`STV-408`（各 spec の受け入れテストと重複する場合は本 spec で**統合・ギャップ埋め**）。

## Alignment with Product Vision

観測可能性は機能と同等にテスト対象とする。

## Requirements

### Requirement 1 — カバレッジ方針

**User Story:** As a **保守者**, I want **API と Engine の主要ログがテストで検証される**こと, so that **リファクタでログが消えない**。

#### Acceptance Criteria — Requirement 1

1. WHEN **本仕様が完了する** THEN **次がテストでカバーされる**（既存テストを含む）:
   - HTTP: リクエスト開始/完了（主要フィールド）
   - Engine: workflow/state の少なくとも 1 経路
2. WHEN **Warning / Error のログがある** THEN **それぞれ最低 1 ケース**がテストで検証される（該当機能が `STV-405` 等で未実装の場合は **スキップ理由を tasks に明記**）。

### Requirement 2 — 安定性

**User Story:** As a **CI 担当**, I want **テストがフレークしない**こと, so that **信頼できる**。

#### Acceptance Criteria — Requirement 2

1. WHEN **`dotnet test` を実行する** THEN **ログ関連テストがタイムゾーンや並列実行に依存しない**（固定時刻 or フェイク）。

### Requirement 3 — ドキュメント

**User Story:** As a **新メンバー**, I want **どのテストがログを担保するか分かる**こと, so that **変更時に更新できる**。

#### Acceptance Criteria — Requirement 3

1. WHEN **完了する** THEN **`docs/` またはテストクラスの README コメントに 1 段落のマップ**（任意・最小）。

## Out of Scope

- E2E でのログ検証（別チケット可）。
- カバレッジ 100%。

## References

- `api/Statevia.Core.Api.Tests/Hosting/`
- `engine/Statevia.Core.Engine.Tests/`
