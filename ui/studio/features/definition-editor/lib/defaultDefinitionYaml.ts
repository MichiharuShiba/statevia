/** Definition Editor 初期テンプレート（sleep → fork/join → end）。 */
export const defaultDefinitionYaml = `version: 1
workflow:
  name: DefinitionMinimal
nodes:
  - name: start
    type: start
    next: slowStep
  - name: slowStep
    type: action
    action: sleep
    input:
      duration: 5s
    next: fork1
  - name: fork1
    type: fork
    branches: [branchLeft, branchRight]
  - name: branchLeft
    type: action
    action: noop
    next: join1
  - name: branchRight
    type: action
    action: sleep
    input:
      duration: 5s
    next: join1
  - name: join1
    type: join
    mode: all
    next: endNode
  - name: endNode
    type: end
`;
