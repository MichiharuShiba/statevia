# U10: nodes / states の判別方法

**前提**（modification-plan Phase 5, U7.1）: **nodes 形式**は Engine 内で **states ベースの CompiledWorkflowDefinition に変換**する。API の `POST /v1/definitions` は YAML を受け取り、**nodes 形式か states 形式かを判別**したうえで、nodes の場合は Engine の変換入りローダーを呼び、states の場合は従来のローダーを呼ぶ。

- **nodes 形式**（.workspace-docs/30_specs/20_done/v2-definition-spec.md）: ルートに **version**（整数）, **workflow**, **nodes**（配列）を持つ。各要素は id, type, next, branches 等。
- **states 形式**（.workspace-docs/30_specs/20_done/v2-workflow-definition-spec.md）: ルートに **workflow**（name, initialState）, **states**（オブジェクト：StateName → 定義）を持つ。

本ドキュメントでは **API が YAML を受け取ったとき、nodes と states をどう判別するか** を議論する。

---

## 1. 論点

- **判別のタイミング**: POST で body を受け取った直後、パース後にどのキーを見るか。
- **判別ルール**: ルートのキー（`version` の有無、`nodes` の有無、`states` の有無）のどれを優先するか。両方ある場合の優先順位。
- **definition-spec との対応**: definition-spec の nodes スキーマは required: version, workflow, nodes。states 形式にはルートの `version` は必須でない。`nodes` は nodes 形式のみが持つ配列、`states` は states 形式のみが持つオブジェクト。

---

## 2. 判別方法の選択肢

| 案    | 判別ルール                                                                                                     | 説明                                                                            | メリット                             | デメリット                                                    |
| ----- | -------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------- | ------------------------------------ | ------------------------------------------------------------- |
| **A** | **ルートに `nodes` が存在し配列なら nodes 形式、さもなくば states 形式**                                       | パース後、ルートに `nodes` キーがあり、その値が配列なら nodes とする。          | 定義仕様と一致。実装が単純。         | states 形式で誤って `nodes` を付けた場合に nodes 扱いになる。 |
| **B** | **ルートに `version` が存在し 1 なら nodes 形式、さもなくば states 形式**                                      | definition-spec の nodes は `version: 1` を必須とする。                         | version で明示できる。               | states 形式でも `version` を付けると nodes と誤判する可能性。 |
| **C** | **ルートに `states` がオブジェクトなら states 形式、`nodes` が配列なら nodes 形式。両方ある場合は nodes 優先** | 両キーを見て、nodes 配列があれば nodes、なければ states オブジェクトで states。 | 両形式の典型形をはっきり区別できる。 | ルールが 1 段増える。                                         |
| **D** | **明示フィールドで指定**（例: ルートに `format: "nodes"` または `format: "states"`）                           | API は body の `format` を信頼する。またはクエリ `?format=nodes` で上書き。     | 曖昧さがない。                       | 既存 YAML にフィールドを足す必要。クライアントの変更。        |

**補足**: definition-spec の nodes スキーマでは **required: version, workflow, nodes** であり、**states キーは存在しない**。workflow-definition-spec の states 形式では **workflow と states** であり、**nodes キーは存在しない**。よって、正常な YAML であれば「ルートに `nodes` が配列で存在するかどうか」で一意に判別できる（**案 A**）。`version` だけに頼ると、states 側で version をメタデータとして持つ場合に紛れる可能性があるため、**nodes の有無で判別する案 A（または両方見る案 C）** が仕様と整合する。

---

## 3. 推奨の方向性

- **判別**: **案 A** を採用する。ルートに **`nodes` キーが存在し、かつその値が配列** なら **nodes 形式** とみなす。そうでなければ **states 形式** とみなす。
- **両方ある場合**: ルートに `nodes`（配列）と `states`（オブジェクト）が両方存在する YAML は仕様上想定しない。実装では **`nodes` を先にチェック** し、配列なら nodes 形式として扱えばよい。states のみの YAML には `nodes` がないため、states 形式として扱われる。
- **不正 YAML**: どちらの形式でもない（`nodes` も `states` もない、または型が違う）場合は、ロード・検証時にエラーになる。判別ロジックでは「nodes でなければ states」とし、states としてロードに失敗したら 400 等で返す。

---

## 4. 実装メモ

1. YAML をパースしてルートを取得。
2. `root["nodes"]` が存在し、かつ `Array.isArray(root["nodes"])`（または C# なら `root["nodes"]` が配列型）なら **nodes 形式** → Engine の nodes 用ローダー（変換入り）を呼ぶ。
3. それ以外は **states 形式** → 既存の states 用ローダーを呼ぶ。
4. 検証・コンパイルで失敗した場合は 400 Bad Request（または 422）でエラー内容を返す。

---

## 5. 決定事項・オープンな論点（記入用）

### 5.1 決定事項

| 事項         | 決定内容                                                                                 |
| ------------ | ---------------------------------------------------------------------------------------- |
| 判別方法     | **ルートに `nodes` が存在しその値が配列なら nodes 形式、さもなくば states 形式**（決定） |
| 両方ある場合 | どちらの形式が採用されたか不透明なのでエラーを返却する（決定）                           |
| 不正時       | nodes でも states でもない場合は その時点でエラーを返却する（決定）                      |

### 5.2 オープンな論点

- 将来、`format: "nodes"` を明示指定した場合に判別を上書きするか（API の拡張）。
- Content-Type やクエリ（`?format=nodes`）で形式を指定するか。Phase 5 では YAML body の構造のみで判別する。

以上を、U10 nodes / states の判別方法の議論とする。
