import { notFound } from 'next/navigation';
import { adminGet } from '@/lib/admin/api';
import { getSiteAudit, getSiteDocument } from '@/lib/admin/site-api';
import type { AdminCategory } from '@/lib/admin/types';
import { EditorPage } from '../_components/EditorPage';
import { HomeEditor } from './HomeEditor';

export default async function HomePage() {
  const [doc, audit, categories] = await Promise.all([getSiteDocument('home'), getSiteAudit('home', 1), adminGet<AdminCategory[]>('/admin/categories')]);
  if (!doc || !(doc.draft ?? doc.published)) notFound();
  const revision = doc.draft?.rowVersion ?? `published-${doc.published?.version}`;
  const options = (categories ?? []).filter((c) => c.isActive).map((c) => ({ id: c.id, name: c.name }));

  return (
    <EditorPage kind="home" doc={doc} audit={audit?.items ?? []}>
      <HomeEditor key={revision} doc={doc} categories={options} />
    </EditorPage>
  );
}
