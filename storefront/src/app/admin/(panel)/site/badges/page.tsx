import { notFound } from 'next/navigation';
import { getSiteAudit, getSiteDocument } from '@/lib/admin/site-api';
import { EditorPage } from '../_components/EditorPage';
import { BadgesEditor } from './BadgesEditor';

export default async function BadgesPage() {
  const [doc, audit] = await Promise.all([getSiteDocument('badges'), getSiteAudit('badges', 1)]);
  if (!doc || !(doc.draft ?? doc.published)) notFound();
  const revision = doc.draft?.rowVersion ?? `published-${doc.published?.version}`;

  return (
    <EditorPage kind="badges" doc={doc} audit={audit?.items ?? []}>
      <BadgesEditor key={revision} doc={doc} />
    </EditorPage>
  );
}
