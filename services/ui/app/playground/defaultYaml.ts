/** Playground 初期テンプレート（nodes 形式・Core-API コンパイル検証と整合）。 */
export const defaultPlaygroundYaml = `version: 1
workflow:
  name: PlaygroundMinimal
nodes:
  - id: start
    type: start
    next: endNode
  - id: endNode
    type: end
`;
