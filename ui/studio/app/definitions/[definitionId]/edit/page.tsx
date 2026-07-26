import { DefinitionEditorPageClient } from "@/features/definition-editor/ui/DefinitionEditorPageClient";

type DefinitionEditPageProps = {
  params: Promise<{
    definitionId: string;
  }>;
};

/**
 * Definition 編集ページ（薄い route wrapper）。
 */
export default async function DefinitionEditPage({ params }: Readonly<DefinitionEditPageProps>) {
  const { definitionId } = await params;
  return <DefinitionEditorPageClient definitionId={definitionId} />;
}
