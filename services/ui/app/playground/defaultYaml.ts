/** Playground 初期テンプレート（delay5s → fork/join → end）。 */
export const defaultPlaygroundYaml = `version: 1
workflow:
  name: PlaygroundMinimal
nodes:
  - id: start
    type: start
    next: slowStep
  - id: slowStep
    type: action
    action: delay5s
    next: fork1
  - id: fork1
    type: fork
    branches: [branchLeft, branchRight]
  - id: branchLeft
    type: action
    action: noop
    next: join1
  - id: branchRight
    type: action
    action: delay5s
    next: join1
  - id: join1
    type: join
    mode: all
    next: endNode
  - id: endNode
    type: end
`;
