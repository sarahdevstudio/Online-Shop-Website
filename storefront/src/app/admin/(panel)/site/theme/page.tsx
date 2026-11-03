import { notFound } from 'next/navigation';
import { getSiteAudit, getSiteDocument } from '@/lib/admin/site-api';
import { EditorPage } from '../_components/EditorPage';
import { ThemeEditor } from './ThemeEditor';

export default async function ThemePage() {
  const [doc, audit] = await Promise.all([getSiteDocument('theme'), getSiteAudit('theme', 1)]);
  if (!doc || !(doc.draft ?? doc.published)) notFound();
  const revision = doc.draft?.rowVersion ?? `published-${doc.published?.version}`;

  return (
    <EditorPage kind="theme" doc={doc} audit={audit?.items ?? []}>
      <ThemeEditor key={revision} doc={doc} />
    </EditorPage>
  );
}
