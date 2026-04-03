# 未決定事項一覧（v2 改修計画）

- Version: 1.0.0
- 更新日: 2026-04-02
- 対象: v2 改修に関する未決定・決定の整理
- 関連: `.workspace-docs/40_plans/10_in-progress/v2-modification-plan.md`

---

以下は改修計画で「検討」「決定」「別途判断」等とされている項目です。ご判断いただき、決まった内容を計画に反映してください。

---

## 1. リポジトリ・配置

| #   | 事項                                          | 選択肢・補足                                                                                                                                                |
| --- | --------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1.1 | **Core-Engine / Core-API のディレクトリ配置** | 現行 `core/` 配下に両方置くか、`engine/` と `api/` をリポジトリ直下の同階層に分けるか。                                                                     |
| 1.2 | **Statevia.Core の分離方法**                  | (A) 既存 `Statevia.Core` をプロジェクト名ごと `Statevia.Core.Engine` にリネームする。(B) `Statevia.Core.Engine` を新規作成し、Engine 関連コードを移動する。 |

### 1.1 補足（engine と api を分けた場合のユーザー State の取り込み）

**取り込めます。** Engine は `IStateExecutor` と `IStateExecutorFactory` だけを参照し、ユーザー定義の `IState<TIn, TOut>` 実装は参照しません。流れは次のとおりです。

- ユーザーは `IState<TIn, TOut>` を実装した独自クラスを **API 側**（または API が参照するアセンブリ）に用意する。
- API 側で、状態名 → `IStateExecutor` の辞書を組み立てる。各要素は `DefaultStateExecutor.Create(userState)` でユーザーの `IState` をラップする。
- その辞書で `DictionaryStateExecutorFactory` を作り、`DefinitionCompiler` に渡してコンパイルする。生成された `CompiledWorkflowDefinition` にはそのファクトリが含まれる。
- API が `Engine.Start(CompiledWorkflowDefinition)` を呼ぶ。Engine は定義内の `StateExecutorFactory.GetExecutor(stateName)` で `IStateExecutor` を取得し、`ExecuteAsync(ctx, object?, ct)` だけを呼ぶため、**Engine はユーザーの型（TIn/TOut）を知らない**。
- したがって、engine と api を別プロジェクト／別アセンブリに分けても、API が「ユーザー State の登録 → コンパイル → Engine に渡す」を行う限り、ユーザー独自実装の State をそのまま利用できます。

### 決定事項（1. リポジトリ・配置）

1.1 engine/とapi/はリポジトリ直下に配置し、別ソリューション(リポジトリ直下にソリューションファイルを置かない)として独立させます。api/はengineを参照(将来的にはnugetなどでパッケージとして取り込む)する

1.2 (A)Statevia.Core.Engine にリネームします。

---

## 2. Core-Engine（C#）

| #   | 事項                         | 選択肢・補足                                                                                                                                                        |
| --- | ---------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 2.1 | **名前空間**                 | `Statevia.Core.Engine` に統一するか、現行の `Statevia.Core` のままにするか。                                                                                        |
| 2.2 | **NuGet パッケージ化**       | Core-Engine を NuGet パッケージ（例: `Statevia.Core.Engine`）として発行するか、プロジェクト参照のみとするか。                                                       |
| 2.3 | **永続化用インターフェース** | Engine 内に `IWorkflowRepository` 等の抽象を定義し、実装は API 側に置くか。それとも Engine は永続化を一切知らず、API が Engine の入出力だけを永続化する形にするか。 |

### 2.2 補足（NuGet 発行時のローカル実行）

NuGet パッケージとして発行する場合でも、**ローカル実行は通常「プロジェクト参照」で行う**とスムーズです。

- **開発時（同一リポジトリ内）**: Core-API は Engine を **プロジェクト参照**（`<ProjectReference Include="..\Statevia.Core.Engine\..." />`）で参照する。`dotnet run` や F5 では Engine のソースがそのままビルドに含まれるため、Engine を直したら API を再実行するだけで反映される。NuGet のバージョン管理や復元は不要。
- **NuGet を使う場合のローカル実行**: (1) **パッケージ参照に切り替える** — Core-API の csproj で `PackageReference` にし、`dotnet restore` で nuget.org または社内フィードから取得。ローカルで「パッケージとしての動き」を試せるが、Engine のコードを変えるたびにパッケージを再発行する必要がある。(2) **ローカル NuGet ソース** — `dotnet pack` でパッケージを生成し、ローカルフォルダ（例: `./packages-local`）に配置。nuget.config でそのフォルダを source に追加し、Core-API はそのソースから同じバージョンを参照する。CI や他サービスから「パッケージ前提」の動きを再現したいときに使う。
- **運用の分け方**: 同じソリューションで「開発時は ProjectReference、CI/本番ビルドでは PackageReference」にすることも可能（例: Directory.Build.props で条件付き参照、または API を別リポジトリに分離しそのリポジトリでは常に PackageReference）。

まとめ: **ローカル実行 = プロジェクト参照** にしておけば、NuGet 発行の有無にかかわらず普段の開発に支障はありません。

### 決定事項（2. Core-Engine）

2.1 Statevia.Core.Engineにリネームする

2.2 将来的にはnugetパッケージを発行するが、開発時点ではローカルのプロジェクト参照とする

2.3 engine は永続化などのインターファイスに関わる知識は持たず、純粋なエンジンとしてのドメインを持たせる

---

## 3. Core-API（C#）

| #   | 事項                                         | 選択肢・補足                                                                                                                   |
| --- | -------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| 3.1 | **PostgreSQL アクセス手段**                  | Npgsql（生 SQL / Dapper）とするか、EF Core とするか。                                                                          |
| 3.2 | **スキーマ作成方法**                         | マイグレーション（EF Core 等）で管理するか、SQL スクリプト（例: `sql/001_init.sql`）で管理するか。                             |
| 3.3 | **IWorkflowEngine の DI 登録**               | シングルトンで 1 プロセスに 1 エンジンとするか、スコープ（リクエスト単位等）とするか。実行中インスタンスの保持方法に影響する。 |
| 3.4 | **GET /workflows/{id} のデータソース**       | 常に Engine.GetSnapshot から返すか、DB のスナップショット（projection）から返すか、または両方の使い分け条件を決めるか。        |
| 3.5 | **GET /workflows/{id}/graph のデータソース** | 常に Engine.ExportExecutionGraph から返すか、DB の execution_graph_snapshots から返すか、または両方の使い分け条件を決めるか。  |
| 3.6 | **認証**                                     | v2 で認証（API Key / JWT / その他）を入れるか、まずは認証なしとするか。                                                        |

### 3.3 補足（DI スコープと cancel / resume の判別）

**スコープを「リクエスト単位」にすると、cancel も resume も正しく動きません。** 現行の `WorkflowEngine` は、プロセス内の `ConcurrentDictionary<string, WorkflowInstance> _instances` に workflowId ごとの実行状態を保持しています。

- **シングルトン（アプリケーション単位）**: エンジンが 1 つだけなので、`CancelAsync(workflowId)` は同じエンジン内の `_instances` から該当インスタンスを探せます。後続リクエストでも同じエンジンが注入されるため、**cancel は問題なく判別できます**。resume（`PublishEvent`）も、その 1 つのエンジンが持つ `_eventProviders` に届きます。※現行 `PublishEvent(eventName)` は workflowId を取らず「そのエンジン内の全ワークフローにブロードキャスト」するため、「特定 workflow だけ resume」には未対応。必要なら Engine に `PublishEvent(workflowId, eventName)` を検討。
- **スコープ（例: リクエスト単位）**: リクエストごとに別の `IWorkflowEngine` が作られます。`POST /workflows` で Start したリクエストのスコープが終わると、そのエンジンと `_instances` は破棄されます。あとから `POST /workflows/{id}/cancel` や events を送るリクエストでは**別のエンジン**が注入され、そのエンジンの `_instances` は空なので、workflowId で**判別できず cancel/resume できません**。複数の `IWorkflowEngine` を「workflowId でどれか選ぶ」ようにするには、workflowId → エンジンインスタンスの永続的なマップが別途必要になり、現行の in-memory 設計とは合いません。

結論: **cancel / resume を HTTP リクエストをまたいで動かすには、IWorkflowEngine はシングルトン（アプリケーション単位）にする必要があります。** 3.3 の「スコープをアプリケーション単位」はこの前提に沿った正しい選択です。

### 決定事項（3. Core-API）

3.1 複雑な抽出などはないのでEF Coreとする

3.2 EF Coreでマイグレーション管理する

3.3 IWorkflowEngineのDIはシングルトンとする、APIの呼び出しから対象を判別できるようにPublishEvent(workflowId, eventName)も作成する。
PublishEvent(workflowId)も残す。APIも両方に対応するようにする。

3.4 原則、enjineに対してpullはしない、DBから取得する

3.5 execution_graph_snapshotsから取得する

3.6 まずは認証なしとする。今後認証方式を検討して実装する。

---

## 4. 永続化・スナップショット

| #   | 事項                                           | 選択肢・補足                                                                                                                                                                                                              |
| --- | ---------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 4.1 | **workflow_events の用途**                     | 監査ログとしてのみ使うか、Engine の状態を再現するためのイベントソースとしても使うか。                                                                                                                                     |
| 4.2 | **execution_graph_snapshots の更新タイミング** | Engine のコールバック／イベントで駆動するか、定期的に GetSnapshot / ExportExecutionGraph でポーリングして更新するか。Engine がコールバックを公開するかどうかも含む。                                                      |
| 4.3 | **再起動時の復元**                             | Core-API 再起動時に、status=Running の workflow を Engine に再投入するか、スナップショットから復元するか、再起動時は復元せず「Running は再起動で失効」とするか。復元する場合、Engine に「状態のロード」API を追加するか。 |

### 4.1 補足（workflow_events をイベントソースとして使う場合に考慮すること）

#### イベントの発生元と永続化の責務

現行 Engine は「メモリ上で状態を更新」するだけで、発生したイベント列を返しません。イベントソースにするなら、(1) **Engine が「発生したイベント列」を API に返す**（例: 1 コマンド → Event[] を返す Decide 的な API）、(2) **Engine がコールバック／イベントで API に通知し、API が append**、のいずれかが必要です。API が Engine の振る舞いを解釈して「相当するイベント」を独自に生成すると、Engine とログの不整合や二重実装のリスクがあります。

#### 順序保証

`workflow_id` 単位で `seq` を厳密に単調増加にし、append-only にします。並行して同じ workflow に append する場合は、楽観ロック（例: 期待する seq や version を条件に INSERT）で競合を検知し、409 等で返す設計にします。

#### スキーマの拡張

`.workspace-docs/30_specs/20_done/v2-db-schema.md` の workflow_events は `id, workflow_id, seq, type, payload_json, created_at` のみです。`docs/core-events-spec` の EventEnvelope をそのまま保存するなら、`event_id`, `actor_kind`, `actor_id`, `correlation_id`, `causation_id`, `schema_version` などを列に持つか、`payload_json` に含めるかを決めます。監査・検索のしやすさとマイグレーションのコストのトレードオフです。

#### リプレイと状態の再計算

状態は「イベント列を先頭から reducer に適用した結果」として一意に決まるようにします。`docs/core-reducer-spec` のような純粋な reducer であれば、同じイベント列から同じ ExecutionState が得られます。再起動時の復元やデバッグでは、必要に応じて「直近スナップショット + その後のイベントのみリプレイ」する最適化を検討します。

#### projection（workflows / execution_graph_snapshots）との一貫性

イベントを append したら、同じトランザクションまたは直後に reducer を適用して workflows や execution_graph_snapshots を更新します。イベントソースが正なら projection は常にリプレイで再計算可能であるべきで、不整合時は「イベントから再計算した結果で projection を上書き」する修復手段があると安全です。

#### イベントの不変性とスキーマ進化

イベントは「事実」なので、既存レコードの type や payload の意味は変えません。破壊的変更が必要な場合は、新しい `type` や `schema_version` で書き、リプレイ時にバージョンに応じて解釈するようにします。

#### トランザクション

1 回のコマンドで複数イベントを出す場合、それらは **1 トランザクションでまとめて append** し、seq の連続性を保ちます。部分だけ書かれた状態を防ぎます。

### 4.1 補足（イベントソースを別テーブルとして保持する方法を検討して分ける場合に考慮すること）

#### 分離の目的と境界

イベントソースを「専用テーブル」にし、workflows / execution_graph_snapshots 等の **projection（読み取り用テーブル）** と分ける場合、**イベントテーブル = 正（Single Source of Truth）**、projection = イベントを適用して導出した結果、と明確にします。監査・リプレイ・デバッグはイベントテーブルを参照し、通常の GET は projection を参照する形にします。

#### テーブル構成の選択

(1) **1 テーブルで全イベント**（例: `event_store`）: 列に `workflow_id`（または aggregate_id）を持ち、ワークフロー単位のリプレイは `WHERE workflow_id = ? ORDER BY seq` で取得。(2) **集約（ワークフロー）ごとにテーブル**（例: 動的テーブル名）: 通常は運用が重いため、まずは (1) の 1 テーブル + workflow_id で分ける構成を推奨。イベントテーブル名は `event_store` / `workflow_events` のどちらにするか、既存の workflow_events を「イベント専用」に使うか、新規に `event_store` を用意するかを決めます。

#### スキーマと seq のスコープ

イベント専用テーブルには、少なくとも `id`, `workflow_id`, `seq`, `type`, `payload_json`, `created_at` を用意します。`seq` は **workflow_id 単位の単調増加** にするとリプレイが単純です。グローバル seq にする場合は、リプレイ時に workflow_id でフィルタしつつ順序を保つ必要があります。EventEnvelope のメタデータ（event_id, actor, correlation_id, schema_version）は列にするか payload に含めるかを 4.1 のスキーマ方針に合わせます。

#### 同一 DB 内で分ける場合のトランザクション

イベントテーブルと projection テーブルが **同じ DB** にある場合、**1 トランザクションで「イベント append + reducer 適用 + projection 更新」** を行うと一貫性を保ちやすくなります。イベントだけ先に commit し、projection は別トランザクションで更新する場合は、失敗時に projection が遅れる or 不整合になるため、リトライや「イベントから再計算」で修復する手順を用意します。

#### 読み取りパフォーマンスとインデックス

イベントテーブルは **append 専用**（INSERT のみ）、**リプレイ時は workflow_id + seq で範囲取得** が主なアクセスパターンです。`(workflow_id, seq)` のユニーク制約とインデックスを張ります。時間範囲での検索や監査用には `created_at` のインデックスを検討。projection テーブルは通常の GET 用なので、イベントテーブルとはアクセスパターンが異なり、分離するとそれぞれに合ったチューニング（例: イベントテーブルのパーティション）がしやすくなります。

#### projection の更新主体（プロジェクター）

イベントを append したあと、誰が projection を更新するかを決めます。(1) **同期**: コマンド処理の同一トランザクション内で reducer を適用し projection を更新（推奨。シンプルで一貫性が取りやすい）。(2) **非同期**: 別プロセス／ワーカーがイベントテーブルをポーリングまたはサブスクライブし、projection を更新。その場合は eventual consistency になり、読む側が「最新イベントまで反映済み」かどうかを扱う必要があります。

#### リテンションとアーカイブ

イベントテーブルは監査・再計算用に長期保持し、projection は「現在の状態」だけ持つ運用にすることがあります。分離していると、イベントテーブルだけ別のリテンションやアーカイブ先（別ストレージ・冷たい領域）に移すポリシーを適用しやすくなります。

#### 既存の workflow_events との関係

すでに `workflow_events` がある場合、「workflow_events = イベントソース専用」にして projection は workflows / execution_graph_snapshots にだけ持つか、あるいは新規に `event_store` テーブルを追加し、workflow_events は監査用の冗長書きや段階的移行用に残すかを決めます。新規に event_store を用意する場合は、二重書き期間・バックフィル・カットオーバーの手順を検討します。

### 決定事項（4. 永続化・スナップショット）

4.1 event_store を用意してworkflow_eventsは監査用、event_storeはイベントソース、workflows / execution_graph_snapshotsはprojection（読み取り用テーブル）としてそれぞれの役割を分ける

4.2 エンジンがイベントを公開し、APIがそれを購買する

4.3 再起動ポリシーを用意して、ユーザーの設定に準じて再起動時の挙動(再実行、復元、失効)を制御する。

---

## 5. TypeScript Core-API（services/core-api）

| #   | 事項                | 選択肢・補足                                                                 |
| --- | ------------------- | ---------------------------------------------------------------------------- |
| 5.1 | **v2 移行後の扱い** | リポジトリから削除するか、レガシーとして残すか（参照しないがコードは残す）。 |

### 決定事項（5. TypeScript Core-API）

5.1 services/core-apiは削除する。ただし、legacyブランチ又はタグを使って一旦Gitで保存する。

---

## 6. UI

| #   | 事項                    | 選択肢・補足                                                                                        |
| --- | ----------------------- | --------------------------------------------------------------------------------------------------- |
| 6.1 | **旧 /executions 依存** | 削除して /workflows のみにするか、/workflows へのリダイレクトやマッピングレイヤーを一時的に残すか。 |

### 決定事項（6. UI）

6.1 /executionsは削除する

---

## 7. 定義形式（Phase 5 を実施する場合）

| #   | 事項                 | 選択肢・補足                                                                                                                |
| --- | -------------------- | --------------------------------------------------------------------------------------------------------------------------- |
| 7.1 | **nodes 形式の扱い** | nodes 形式を Core-Engine 内で states ベースの CompiledWorkflowDefinition に変換するか、Engine 内で nodes を直接解釈するか。 |

### 7.1 補足（nodes 形式の扱い — 変換 vs 直接解釈）

#### 二つの選択肢の意味

**(A) nodes → states に変換**: nodes 形式の YAML（`version`, `workflow`, `nodes[]`。各 node は `id`, `type`（start/action/wait/fork/join/end）, `next`, `branches`, `event`, `action` 等）を読み込み、**states 形式の WorkflowDefinition に変換**してから、既存の DefinitionCompiler で CompiledWorkflowDefinition を生成し、Engine の実行コア（FSM, JoinTracker, Scheduler 等）に渡す。Engine の実行パスは現行の「states + Fact 駆動」のまま 1 本。(B) **Engine が nodes を直接解釈**: Engine が nodes 形式をネイティブに受け付け、CompiledWorkflowDefinition とは別の「nodes 用のコンパイル結果」で実行する。実行ループが「現在ノード id」「next / branches」を直接参照する形になり、既存の states/FSM と並行して別のランタイムを持つか、内部で states 相当に落とし込むかが必要。

#### 変換する場合の対応関係（nodes → states）

- node の `id` ＝ state 名（一意）。
  - `type: start` → 初期状態として 1 つだけ指定し、`next` 先を初期遷移に。
  - `type: action` → state に `on: { Completed: { next: ... } }` を設定。`onError.next` は将来拡張で Failed 時の遷移として扱うか検討。
  - `type: wait` → state に `wait: { event: ... }` と `on: { <event名>: { next: ... } }`。`onTimeout` はタイムアウト用の Fact/イベントにマッピングするか検討。
  - `type: fork` → state に `on: { Completed: { fork: [ ... ] } }`（branches が fork 先の state 名のリスト）。
  - `type: join` → state に `join: { allOf: [ ... ] }` と `on: { Joined: { next: ... } }`。
  - `type: end` → state に `on: { Completed: { end: true } }`。  
    変換は **Engine の Definition レイヤー**（例: NodesToStatesConverter や、nodes 用 DefinitionLoader が WorkflowDefinition を出力する）に置くと、Engine 内部は従来どおり WorkflowDefinition → CompiledWorkflowDefinition → 実行、のまま保てます。API が YAML を渡す際に「nodes 形式」か「states 形式」かを判別し、nodes の場合は Engine の変換入りローダーを呼ぶ形にできます。

#### 変換を選ぶ利点

既存の FSM・JoinTracker・ExecutionGraph・Fact の意味をそのまま利用できる。実行コードの重複がなく、テストや保守が 1 本化する。nodes 固有のフィールド（`onError`, `timeout`, `controls` 等）は、変換時に states 側の表現（将来の Fact や拡張）にマッピングするか、変換レイヤーで解釈して「擬似的な state 遷移」に落とす方針にすればよい。

#### 直接解釈を選ぶ場合の考慮

nodes 専用のコンパイル結果と実行ループを Engine 内に持つと、states 用と nodes 用の二重実装になり、バグ修正や仕様変更を両方に反映する必要が出る。nodes だけの拡張（例: ノード単位のリトライ）を素直に扱える一方、コストは大きい。現行の「states ベースの CompiledWorkflowDefinition に変換する」決定であれば、**変換レイヤーを Engine 内に 1 つ用意し、実行は既存の states パイプラインに統一する**形で足ります。

### 決定事項（7. 定義形式）

7.1 states ベースの CompiledWorkflowDefinitionに変換する

---

## 8. スコープ・優先度

| #   | 事項                                 | 選択肢・補足                               |
| --- | ------------------------------------ | ------------------------------------------ |
| 8.1 | **Phase 4（DB 安定化・再起動復元）** | 実施するか、当面スキップするか。           |
| 8.2 | **Phase 5（nodes 形式サポート）**    | 実施するか、当面 states 形式のみとするか。 |

### 決定事項（8. スコープ・優先度）

8.1 スキップする。ただし実装する前提であることを考慮する

8.2 実施する

---

## 9. その他（計画に明示されていないが実装時に必要）

| #   | 事項                          | 選択肢・補足                                                                           |
| --- | ----------------------------- | -------------------------------------------------------------------------------------- |
| 9.1 | **definitionId の生成規則**   | UUID とするか、別の ID 戦略（例: ナノ秒ベース、シーケンス）とするか。                  |
| 9.2 | **workflowId の生成規則**     | 現行 Engine は 12 桁 ID（GUID の先頭）を使用。v2 でも同じとするか、UUID フルとするか。 |
| 9.3 | **REST API のバージョニング** | パスに `/v1/` 等のバージョン prefix を付けるか、付けないか。                           |

### 9 補足（UUID・短縮ID・独自IDの懸念点）

#### UUID（v4 等）

**懸念**: 128bit のため URL やログが長くなり、手入力・口頭での共有に向かない。衝突は事実上ないが、**時間順序が分からない**（UUID から作成時刻を推測しづらい）。分散環境では時刻同期なしで発行できる利点がある。  
  **考慮**: definitionId / workflowId を UUID にする場合は、一貫して「UUID フル」にし、短縮版を別途表示用に持つかは用途次第。DB の PK や外部キーにはそのまま使える。

#### 短縮ID（現行の 12 桁 = GUID の先頭など）

**懸念**: **衝突確率が UUID より高い**。桁数が少ないほど衝突リスクが増え、データ量や発行頻度が増えると現実問題になりうる。また「何桁にすれば安全か」の根拠を説明する必要がある。URL やログは短く読みやすい一方、グローバル一意性の保証は弱い。  
  **考慮**: 短縮する場合は、十分な桁数（例: 16〜22 文字）や、発行元・時刻を織り込んだ構成にするか、DB のユニーク制約で重複を弾く運用を前提にする。監査や他システムとの連携で「標準的な ID」が求められる場合は UUID の方が無難なことが多い。

#### 独自ID（ナノ秒ベース・シーケンス・スラグ等）

**懸念**: **単一ノード・単一プロセス前提**になりやすい。ナノ秒やシーケンスは、複数 API インスタンスや複数 DB があると重複や順序のずれが起きうる。スラグ（人間可読の `order-123` 等）は一意性の保証と採番の管理が別途必要。カスタム形式にすると、他システムとの連携やログ解析で「何の ID か」の説明コストが増える。  
  **考慮**: 分散環境を想定するなら、UUID や UUID+短縮表示の組み合わせの方が安全。シーケンスを使う場合は DB の sequence や専用の ID 発行サービスで一意性を担保する設計にする。

#### 使い分けの目安

**内部の一意キー（definitionId, workflowId）**: 衝突を避け、分散・監査を考えるなら **UUID** を推奨。短縮は「表示用の別名」としてのみ使うと、一意性と可読性を両立しやすい。
  **表示・URL・ログ**: 長い UUID をそのまま出さず、短縮表示（例: 先頭 8 桁）やスラグを「表示専用」で持つ場合は、**元の UUID との対応を DB で持つ**ようにし、短縮側を PK にしない。

### 決定事項（9. その他）

9.1 UUIDとする、表示用の独自ID(英数字10桁)を持ちUUIDと表示用の独自IDを専用テーブルで管理して表示用の独自IDからUUIDに変換して各テーブルのデータにアクセスする

9.2 UUIDとする、9.1と同様に表示用の独自IDを生成して管理する

9.3 /v1/プリフィックスを付ける

---

## 変更履歴

| 版 | 日付 | 内容 |
|----|------|------|
| 1.0.0 | 2026-04-02 | メタブロック整備。 |

決まった事項はこのファイルに「決定: …」と追記するか、`modification-plan.md` に反映してください。
