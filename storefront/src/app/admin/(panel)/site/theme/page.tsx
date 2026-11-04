import { notFound } from 'next/navigation';
import { getSiteAudit, getSiteDocument } from '@/lib/admin/site-api';
import { requestNonce } from '@/lib/nonce';
import { EditorPage } from '../_components/EditorPage';
import { ThemeEditor } from './ThemeEditor';

export default async function ThemePage() {
  const [doc, audit] = await Promise.all([getSiteDocument('theme'), getSiteAudit('theme', 1)]);
  if (!doc || !(doc.draft ?? doc.published)) notFound();

  return (
    <EditorPage kind="theme" doc={doc} audit={audit?.items ?? []}>
      <ThemeEditor doc={doc} nonce={await requestNonce()} />
    </EditorPage>
  );
}
