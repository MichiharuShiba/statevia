# Tasks: UIデザイン共通化・UX強化

**spec 名**: `ui-design-unification`  
**要件**: `requirements.md`  
**設計**: `design.md`

---

- [x] **T1** — 共通 UI 規約の確定
  - File: `.spec-workflow/specs/ui-design-unification/requirements.md`, `.spec-workflow/specs/ui-design-unification/design.md`
  - 内容: ヘッダー構成、余白、状態表示、CTA 優先度、導線ラベル規約、レイアウトパネル（Header/Main/Side/Action）規約を文書化して固定する
  - 目的: 実装前に画面間の判断基準を統一する
  - _Leverage: `.spec-workflow/specs/ui-hybrid-navigation-refresh/requirements.md`, `.spec-workflow/specs/ui-hybrid-navigation-refresh/design.md`_
  - _Requirements: Requirement 1, Requirement 2, Requirement 3, Requirement 4, Requirement 6_
  - _Definition of Done: 規約が requirements/design の双方に反映され、レビューで追跡できる（完了: 2026-04-24）_

- [x] **T2** — 共通コンポーネントの追加
  - File: `services/ui/app/components/layout/PageShell.tsx`, `services/ui/app/components/layout/PageState.tsx`, `services/ui/app/components/layout/ActionLinkGroup.tsx`, `services/ui/app/components/common/StatusBadge.tsx`
  - 内容: 共通レイアウト・状態表示・導線・ステータス表示コンポーネントを実装する
  - 目的: 画面間の見た目と操作ルールを部品レベルで統一する
  - _Leverage: `services/ui/app/components/Toast.tsx`, `services/ui/app/components/execution/TenantMissingBanner.tsx`, `services/ui/app/lib/statusStyle.ts`_
  - _Requirements: Requirement 1, Requirement 2, Requirement 3_
  - _Definition of Done: 各共通コンポーネントが作成され、型検査を通過する（完了: 2026-04-24）_

- [x] **T3** — ハブ画面への適用
  - File: `services/ui/app/dashboard/DashboardPageClient.tsx`, `services/ui/app/definitions/DefinitionsPageClient.tsx`, `services/ui/app/workflows/page.tsx`（必要に応じて client 分離含む）
  - 内容: ハブ画面を PageShell/PageState/StatusBadge ベースへ置換し、導線表現を統一する
  - 目的: 利用頻度の高い入口画面から一貫性を確立する
  - _Leverage: `services/ui/app/lib/api.ts`, `services/ui/app/lib/types.ts`_
  - _Requirements: Requirement 1, Requirement 2, Requirement 3_
  - _Definition of Done: ハブ画面で共通骨格と状態表示の規約が満たされる（完了: 2026-04-24）_

- [ ] **T4** — 詳細・専用画面への適用
  - File: `services/ui/app/workflows/[workflowId]/page.tsx`, `services/ui/app/workflows/[workflowId]/run/page.tsx`, `services/ui/app/workflows/[workflowId]/graph/page.tsx`, `services/ui/app/definitions/[definitionId]/edit/page.tsx`, `services/ui/app/components/execution/ExecutionDashboard.tsx`, `services/ui/app/components/execution/ExecutionHeader.tsx`
  - 内容: detail/run/graph/edit のヘッダー・戻り導線・状態表示を共通ルールに揃える
  - 目的: ハブ外の画面でも同じ操作感を担保する
  - _Leverage: `services/ui/app/components/execution/ExecutionDashboard.tsx`, `services/ui/app/components/nodes/NodeGraphView.tsx`_
  - _Requirements: Requirement 1, Requirement 2, Requirement 3_
  - _Definition of Done: 専用画面で共通導線・共通状態表示が機能し、既存操作を阻害しない_

- [ ] **T5** — SP レイアウト適用
  - File: `services/ui/app/**`（上記対象画面の className / レイアウト定義）
  - 内容: SP での単一カラム順序（Header -> ContextSummary -> Feedback -> Main -> SubActions）を適用する
  - 目的: PC/SP で意味構造を揃え、モバイル運用時の迷いを減らす
  - _Leverage: `services/ui/app/layout.tsx`, 既存 Tailwind utility class_
  - _Requirements: Requirement 4_
  - _Definition of Done: SP 表示で規定順序が維持され、主要 CTA が画面上部で認識できる_

- [ ] **T6** — `playground` 導線整理
  - File: `services/ui/app/**`（ナビゲーションリンク定義箇所）, `.spec-workflow/specs/ui-hybrid-navigation-refresh/*.md`（必要に応じて注記）
  - 内容: 新 UI 導線上の `playground` リンク/案内を整理し、主要導線から除外する
  - 目的: 遷移導線を一本化し、利用者の判断負荷を下げる
  - _Leverage: 既存の route 構成_
  - _Requirements: Requirement 5_
  - _Definition of Done: ハブ・詳細・専用画面で `playground` 依存の主要導線が存在しない_

- [ ] **T7** — テストと品質確認
  - File: `services/ui/tests/**`, `services/ui/app/**` 変更対象
  - 内容: 共通骨格、状態表示、導線ラベル、SP 表示順序のテストを追加/更新し、型検査を実施する
  - 目的: 共通化による UI 回帰を防ぐ
  - _Leverage: 既存 UI テスト基盤_
  - _Requirements: Requirement 1, Requirement 2, Requirement 3, Requirement 4, Requirement 5_
  - _Definition of Done: `npm run test:run` と `tsc --noEmit` が変更範囲で通過する_

- [ ] **T8** — 共通ヘッダーのブランドアイコン表示仕様を反映
  - File: `.spec-workflow/specs/ui-design-unification/requirements.md`, `.spec-workflow/specs/ui-design-unification/design.md`, `services/ui/app/layout.tsx`
  - 内容: `icon.png` を共通ヘッダーへ表示する仕様とインターフェースを確定し、実装対象を明文化する
  - 目的: 画面遷移時のブランド一貫性を担保する
  - _Leverage: `services/ui/app/icon.png`, `services/ui/app/layout.tsx`_
  - _Requirements: Requirement 6_
  - _Definition of Done: 要件・設計・実装対象が一致し、ヘッダーへの適用方針がレビュー可能な状態になる_

- [ ] **T9** — 画面全体トーン（配色）規約の定義と適用計画
  - File: `.spec-workflow/specs/ui-design-unification/design.md`, `services/ui/app/globals.css`, `services/ui/app/layout.tsx`
  - 内容: `icon.png` と整合するトーントークン（ヘッダー、背景、境界、アクセント）を定義し、全画面適用の手順を整理する
  - 目的: アイコンが浮かず、全画面で一体感のある視覚体験を実現する
  - _Leverage: `services/ui/app/icon.png`, `services/ui/app/globals.css`, 既存 Tailwind utility class_
  - _Requirements: Requirement 6_
  - _Definition of Done: トーン規約と適用対象が明文化され、可読性検証ポイントが定義されている_

---

## 実行メモ

- 着手中は `[ ]` を `[-]`、完了後は `[x]` に更新する。
- 推奨実装順は `T1 -> T2 -> T3 -> T4 -> T5 -> T6 -> T8 -> T9 -> T7`。
- 既存 spec（`ui-hybrid-navigation-refresh`）との整合が必要な場合は、差分理由を明記して追記する。
