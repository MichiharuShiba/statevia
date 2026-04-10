# Tasks: O6 サブチケット詳細仕様（STV-413〜STV-418）

**前提:** 要件・設計の正本は `requirements.md` / `design.md` および `.workspace-docs/30_specs/10_in-progress/o6-subtickets_detailed_spec.md`。

---

- [ ] 1. STV-413: 契約ドキュメントへの転記
  - **内容:** projection / トランザクション境界（現状 + 将来案 C）を `docs/statevia-data-integration-contract.md` または `docs/core-api-interface.md` に 1 節追加するか、正本リンクを明記する。
  - **Purpose:** Requirement 1。

- [ ] 2. STV-414: event_store 対応表の公開
  - **内容:** `EventStoreEventType` 3 種の表を `docs/` に載せるか、正本 URL パスを契約文書から辿れるようにする。将来行の TBD 表を維持する。
  - **Purpose:** Requirement 2。

- [ ] 3. STV-415: 再送・べき等の契約転記
  - **内容:** `clientEventId` / `batchId` / リトライを設計メモまたは `docs/` に転記。スキーマ変更が必要なら別チケット見積もりを 1 行で残す。
  - **Purpose:** Requirement 3。

- [ ] 4. STV-416: AGENTS / docs への反映
  - **内容:** Read Model の正は DB、GetSnapshot は in-process（非同期後は最終永続と限らない）を `AGENTS.md` または `docs/` に追記。
  - **Purpose:** Requirement 4。

- [ ] 5. STV-417: nodes 変換仕様との相互リンク
  - **内容:** 段階優先表を `v2-nodes-to-states-conversion-spec.md` の将来拡張に追記、または正本から双方向リンク。
  - **Purpose:** Requirement 5。

- [ ] 6. STV-418: トレーサビリティ最終確認
  - **内容:** `v2-ticket-backlog.md`・`v2-modification-plan.md`・本 spec の参照が一致していることを確認。`concern-o6-decomposition` の任意タスク（modification-plan 更新）が未完了なら完了にする。
  - **Purpose:** Requirement 6。

---

## 完了チェック

1. `STV-413`〜`STV-417` の「文書化」受け入れが満たせる
2. 実装チケット（DB マイグレーション等）は別途起票済みまたは不要と判断できる
