/** Definition Editor 初期テンプレート（sleep → fork/join → end）。 */
export const defaultDefinitionYaml = `version: 1
workflow:
  name: DefinitionMinimal
nodes:
  - id: start
    type: start
    next: slowStep
  - id: slowStep
    type: action
    action: sleep
    input:
      duration: 5s
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
    action: sleep
    input:
      duration: 5s
    next: join1
  - id: join1
    type: join
    mode: all
    next: endNode
  - id: endNode
    type: end
`;
