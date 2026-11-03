import { notFound } from 'next/navigation';
import { getSiteAudit, getSiteDocument } from '@/lib/admin/site-api';
import { EditorPage } from '../_components/EditorPage';
import { SeoEditor } from './SeoEditor';

export default async function SeoPage() {
  const [doc, audit] = await Promise.all([getSiteDocument('seo'), getSiteAudit('seo', 1)]);
  if (!doc || !(doc.draft ?? doc.published)) notFound();

  return (
    <EditorPage kind="seo" doc={doc} audit={audit?.items ?? []}>
      <SeoEditor doc={doc} />
    </EditorPage>
  );
}
