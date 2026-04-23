import { DefinitionEditorPageClient } from "./DefinitionEditorPageClient";

type DefinitionEditPageProps = {
  params: Promise<{
    definitionId: string;
  }>;
};

/**
 * Definition Editor ページ（T10）。
 */
export default async function DefinitionEditPage({ params }: Readonly<DefinitionEditPageProps>) {
  const { definitionId } = await params;
  return <DefinitionEditorPageClient definitionId={definitionId} />;
}
