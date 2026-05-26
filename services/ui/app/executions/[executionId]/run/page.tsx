"use client";

import { useParams } from "next/navigation";
import { useMemo } from "react";
import { ExecutionDashboard } from "../../../components/execution/ExecutionDashboard";
import { ActionLinkGroup } from "../../../components/layout/ActionLinkGroup";
import { PageShell } from "../../../components/layout/PageShell";
import { PageState } from "../../../components/layout/PageState";
import { useUiText } from "../../../lib/uiTextContext";

/**
 * Run 専用ページ。実行操作（Cancel / Resume / Event 送信）をここに集約する。
 */
export default function ExecutionRunPage() {
  const uiText = useUiText();
  const params = useParams();
  const executionId = useMemo(() => {
    const raw = params.executionId;
    const segment = Array.isArray(raw) ? raw[0] : raw;
    return segment ? decodeURIComponent(String(segment)) : "";
  }, [params.executionId]);

  if (!executionId.trim()) {
    return (
      <PageShell
        title={uiText.executionRunPage.title}
        primaryActions={<ActionLinkGroup links={[{ label: uiText.lists.executions, href: "/executions", priority: "primary" }]} />}
      >
        <PageState state="error" message={uiText.executionRunPage.missingExecutionId} />
      </PageShell>
    );
  }

  return (
    <ExecutionDashboard
      key={executionId}
      initialExecutionId={executionId}
      autoLoadOnMount
      headerTitle={uiText.executionRunPage.title}
      executionIdEditable={false}
      comparisonEnabled={false}
      operationsEnabled={true}
      headerNav={
        <ActionLinkGroup
          links={[
            { label: uiText.executionRunPage.navDetail, href: `/executions/${encodeURIComponent(executionId)}`, priority: "primary" }
          ]}
        />
      }
    />
  );
}
