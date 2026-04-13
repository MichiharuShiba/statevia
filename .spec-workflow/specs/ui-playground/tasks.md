# Tasks: UI Playground

**spec 名**: `ui-playground`  
**要件**: `requirements.md`  
**設計**: `design.md`（詳細は `ui-playground-design.md`）

---

- [ ] **P3.0** — `/playground` ルートと定義登録・開始の最小フロー
  - `services/ui/app/playground/`（または合意したパス）にページを追加し、`POST /v1/definitions` / `POST /v1/workflows` を既存 `api` クライアント経由で呼ぶ
  - 成功時に `displayId` / `resourceId` を表示し、Requirement 1 / 2 を満たす
  - _要件: Requirement 1, Requirement 2, Requirement 3（ルート部分）_

- [ ] **P3.1** — 実行ビューの埋め込みと `/playground/run/[displayId]`
  - 既存 `useExecution`・グラフ・タイムライン・Cancel/Event/Resume を Playground レイアウトに組み込む
  - 実行フォーカス用の動的ルートと相互リンク
  - _要件: Requirement 3, Requirement 4, Requirement 5, Requirement 7_

- [ ] **P3.2** — SSE トグルと UX 仕上げ
  - `GET /v1/workflows/{id}/stream` のオプトイン、`GraphUpdated` 後のデバウンス GET（`design.md`）
  - テナントクエリ・エラー表示の統一（`ui-api-auth-tenant-config.md`）
  - _要件: Requirement 6, Non-Functional（Performance）_

---

## メモ

- タスク着手時は `[ ]` を `[-]` にし、完了後に `[x]` と **Implementation Logs**（プロジェクト規約に従う）を更新する。
- Core-API 変更が必要になった場合は **別 spec** または `core-api-interface.md` の改訂タスクに切り出す。
