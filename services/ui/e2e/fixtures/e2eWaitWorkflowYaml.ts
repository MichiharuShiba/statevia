/** Wait 状態で止まる最小 states YAML（E2E 用）。 */
export function e2eWaitWorkflowYaml(workflowName: string): string {
  return `
workflow:
  name: ${workflowName}
states:
  Start:
    on:
      Completed:
        next: WaitNode
  WaitNode:
    wait:
      event: ResumeEvt
    on:
      Completed:
        next: End
  End:
    on:
      Completed:
        end: true
`.trim();
}
