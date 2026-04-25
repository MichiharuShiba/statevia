# Requirements: UIデザイン共通化・UX強化

## Introduction

本仕様は、`ui-hybrid-navigation-refresh` で整備した画面遷移を前提に、UI デザインの共通化と UX 一貫性の向上を定義する。
対象はハブ画面（Dashboard/Definitions/Workflows）と専用画面（Detail/Run/Graph/Edit）全体とし、画面間で同じ操作感を提供する。

## Alignment with Product Vision

- 定義駆動運用を維持し、`Definition` と `Workflow` の導線を崩さずに視覚・操作ルールを統一する。
- API 契約（`/v1/definitions`, `/v1/workflows`, `/v1/workflows/{id}/graph`）を正とし、UI 側で再解釈を増やさない。
- 旧 `playground` 導線は今後の主要導線から除外し、新導線へ一本化する。

## Requirements

### Requirement 1 — 画面骨格の共通化

**ユーザーストーリー:** 利用者として、どの画面でも同じ骨格で情報を読み取りたい。なぜなら、画面ごとの読み替えコストを減らしたいから。

#### Acceptance Criteria — Requirement 1

| No | アクター | きっかけ（ユースケース） | 期待される結果 |
| --- | --- | --- | --- |
| 1 | 利用者 | ハブ画面を開く | タイトル・説明・主要アクション・本文の配置が同一規則で表示される |
| 2 | 利用者 | 専用画面を開く | ヘッダー構成と戻り導線の配置が画面間で一貫している |
| 3 | 開発者 | 新規画面を追加する | 共通コンポーネント（PageShell）を使って同一骨格を再利用できる |
| 4 | 利用者 | Detail/Run/Graph 画面を開く | 主要情報がヘッダー/メイン/補助（サイド）パネルの規約に沿って配置される |

### Requirement 2 — 状態表示（Loading/Empty/Error/Success）の統一

**ユーザーストーリー:** 利用者として、状態表示の意味を迷わず理解したい。なぜなら、画面ごとに解釈が変わると運用判断が遅れるから。

#### Acceptance Criteria — Requirement 2

| No | アクター | きっかけ（ユースケース） | 期待される結果 |
| --- | --- | --- | --- |
| 1 | 利用者 | データ取得中の画面を開く | Loading 表示の見た目・配置・文言トーンが共通化されている |
| 2 | 利用者 | データ 0 件の画面を開く | Empty 表示が共通テンプレートで表示され、次アクション導線が示される |
| 3 | 利用者 | API エラーが発生する | Error 表示と再試行導線が共通パターンで表示される |
| 4 | 利用者 | 操作が成功する | Success 通知が画面間で同一ルール（Toast など）で表示される |

### Requirement 3 — 導線ルールの統一

**ユーザーストーリー:** 利用者として、戻る・進むの操作を毎回迷わず使いたい。なぜなら、画面責務が分かれていても遷移ルールは同じであってほしいから。

#### Acceptance Criteria — Requirement 3

| No | アクター | きっかけ（ユースケース） | 期待される結果 |
| --- | --- | --- | --- |
| 1 | 利用者 | 詳細画面を操作する | 一覧へ戻る導線が常に同じ位置に表示される |
| 2 | 利用者 | Run/Graph/Edit を行き来する | 関連画面への導線ルール（配置・ラベル）が統一される |
| 3 | 開発者 | 画面間リンクを追加する | 共通 ActionLinkGroup によりラベル・優先度ルールを維持できる |

### Requirement 4 — SP（モバイル）レイアウト規約の導入

**ユーザーストーリー:** 利用者として、SP でも必要情報へ順序よくアクセスしたい。なぜなら、PC と同じ意味構造で迷わず操作したいから。

#### Acceptance Criteria — Requirement 4

| No | アクター | きっかけ（ユースケース） | 期待される結果 |
| --- | --- | --- | --- |
| 1 | 利用者 | SP で画面を開く | `Header -> ContextSummary -> Feedback -> Main -> SubActions` の単一カラム順で表示される |
| 2 | 利用者 | SP の Run 画面を操作する | 操作カード、タイムライン、ノード詳細の順序が共通ルールで提示される |
| 3 | 利用者 | SP の Graph 画面を操作する | グラフ本体を優先し、詳細は Sheet/Drawer などで補助表示される |

### Requirement 5 — `playground` 導線の整理

**ユーザーストーリー:** プロダクトオーナーとして、導線を整理して運用導線を一本化したい。なぜなら、現行画面構成に対する学習コストを下げたいから。

#### Acceptance Criteria — Requirement 5

| No | アクター | きっかけ（ユースケース） | 期待される結果 |
| --- | --- | --- | --- |
| 1 | 利用者 | 新UI導線を利用する | `playground` を経由せず、ハブ画面から主要操作が完結する |
| 2 | 開発者 | UI 共通化を進める | 変更対象から `playground` 系画面を外して実装できる |

### Requirement 6 — 共通ヘッダーへのブランドアイコン導入とトーン統一

**ユーザーストーリー:** 利用者として、どの画面でも同じブランドトーンを感じたい。なぜなら、画面遷移しても同一プロダクト上にいる安心感を持ちたいから。

#### Acceptance Criteria — Requirement 6

| No | アクター | きっかけ（ユースケース） | 期待される結果 |
| --- | --- | --- | --- |
| 1 | 利用者 | 任意の画面を開く | 共通ヘッダーに `services/ui/public/brand/icon-mark.png` を基にしたブランドアイコンが表示される |
| 2 | 利用者 | 複数画面を遷移する | ヘッダーとページ全体の配色トーンが一貫し、視覚的な断絶がない |
| 3 | 開発者 | 新規画面を追加する | 共通ヘッダーとトーン規約をそのまま再利用でき、個別調整を最小化できる |

## Non-Functional Requirements

### Clarity

- レイアウト・導線・状態表示の規約を `requirements.md` / `design.md` / `tasks.md` で追跡可能にする。
- 画面責務と UI 規約の対応関係を、図と文章の両方で確認できるようにする。

### Reliability

- 主要画面で状態表示の欠落がないことをテストで検証する。
- 既存の遷移仕様（`ui-hybrid-navigation-refresh`）を壊さずに適用できること。

### Performance

- 共通化により不要な再描画・重複フェッチを増やさないこと。
- SP では初期表示時に主要情報（見出し・状態・主要 CTA）を優先表示すること。

### Usability

- CTA 優先度（主操作 1 つ + 副操作）を画面横断で統一する。
- SP/PC で意味構造を揃え、表示順が異なっても解釈が一致すること。

### Visual Consistency

- ブランドトーンは `services/ui/public/brand/logo-original.png` の色味（ダークネイビー基調 + ブルー/グリーン系アクセント）と整合すること。
- 共通ヘッダー背景・境界・本文背景のコントラストは可読性を維持しつつ、全画面で同一ルールを適用すること。
- アイコン表示時もテキストや主要 CTA の視認性が損なわれないこと。

## Out of Scope

- 新規認証/認可の導入。
- Core-API 契約の変更。
- 画面機能（ユースケース）自体の追加。

## References

- `.spec-workflow/specs/ui-hybrid-navigation-refresh/requirements.md`
- `.spec-workflow/specs/ui-hybrid-navigation-refresh/design.md`
