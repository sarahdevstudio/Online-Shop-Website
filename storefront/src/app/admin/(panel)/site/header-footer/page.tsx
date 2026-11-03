import { notFound } from 'next/navigation';
import { getSiteAudit, getSiteDocument } from '@/lib/admin/site-api';
import { EditorPage } from '../_components/EditorPage';
import { ChromeEditor } from './ChromeEditor';

export default async function HeaderFooterPage() {
  const [doc, audit] = await Promise.all([getSiteDocument('chrome'), getSiteAudit('chrome', 1)]);
  if (!doc || !(doc.draft ?? doc.published)) notFound();

  return (
    <EditorPage kind="chrome" doc={doc} audit={audit?.items ?? []}>
      <ChromeEditor doc={doc} />
    </EditorPage>
  );
}
