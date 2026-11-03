import { revalidatePath, revalidateTag } from 'next/cache';
import { redirect } from 'next/navigation';
import { formatNumber } from '../format';
import { SITE_CACHE_TAG } from '../site-config';
import { startSitePreview } from '../site-preview';
import { failState, okState, submittedFrom, type FormState } from './form-state';
import { adminMessages } from './messages';
import { discardSiteDraft, publishSite, restoreSiteVersion, saveSiteDraft, siteProblemState } from './site-api';
import { siteMessages } from './site-messages';
import { SITE_KIND_PATH, isSiteKind, type PayloadByKind, type SiteKind } from './site-types';

/**
 * The shared body of every site editor's Server Action (docs/10 §9.2). An editor's own `actions.ts` is a one-liner:
 *
 *   export async function saveThemeAction(prev: FormState, formData: FormData) { return runSiteIntent('theme', parseThemeForm, formData); }
 *
 * The form posts `intent` (which button), `rowVersion` (the draft's version the editor loaded, empty when there is no draft)
 * and its own fields. `parse` turns the fields into the payload; the API validates everything again.
 *   save     → PUT draft
 *   preview  → PUT draft, start the preview cookie, go to the shop home page
 *   publish  → PUT draft, then POST publish with the draft's new version
 *   discard  → POST discard (no fields are read)
 */
export const SITE_INTENTS = ['save', 'preview', 'publish', 'discard'] as const;

const IGNORED_FIELDS = ['intent', 'rowVersion'] as const;

function refresh(kind: SiteKind) {
  revalidatePath(SITE_KIND_PATH[kind]);
  revalidatePath('/admin/site');
  revalidatePath('/admin/site/audit');
}

/** After a publish or restore: the storefront's cached configuration is dropped right away (other instances converge in 60 s). */
function expireStorefrontConfig() {
  revalidateTag(SITE_CACHE_TAG, { expire: 0 });
}

export async function runSiteIntent<K extends SiteKind>(kind: K, parse: (formData: FormData) => PayloadByKind[K], formData: FormData): Promise<FormState> {
  const intent = String(formData.get('intent') ?? '');
  const rowVersion = String(formData.get('rowVersion') ?? '') || null;
  const submitted = submittedFrom(formData, ['currentPassword', 'newPassword', 'password', ...IGNORED_FIELDS]);

  if (intent === 'discard') {
    if (!rowVersion) return failState(siteMessages.messages.noDraft);
    const result = await discardSiteDraft(kind, rowVersion);
    if (!result.ok) return siteProblemState(result.problem, submitted);
    refresh(kind);
    return okState(siteMessages.messages.discarded);
  }

  if (!(SITE_INTENTS as readonly string[]).includes(intent)) return failState(adminMessages.failed, submitted);

  const saved = await saveSiteDraft(kind, parse(formData), rowVersion);
  if (!saved.ok) return siteProblemState(saved.problem, submitted);
  const newVersion = saved.body.draft?.rowVersion ?? null;

  if (intent === 'save') {
    refresh(kind);
    return okState(siteMessages.messages.savedDraft);
  }

  if (intent === 'preview') {
    refresh(kind);
    if (!(await startSitePreview())) return failState(siteMessages.messages.previewUnavailable, submitted);
    redirect('/');
  }

  if (!newVersion) return failState(adminMessages.failed, submitted);
  const published = await publishSite(kind, newVersion);
  refresh(kind); // the draft was saved even when publishing is refused: the editor shows it
  if (!published.ok) {
    const state = siteProblemState(published.problem, submitted);
    return state.conflict || Object.keys(state.errors).length > 0 ? state : failState(siteMessages.messages.publishFailedAfterSave, submitted);
  }
  expireStorefrontConfig();
  const version = published.body.published?.version;
  return okState(siteMessages.messages.publishedAfterSave(version ? formatNumber(version) : ''));
}

/** Restore one version into the draft (and publish it right away when asked). */
export async function runSiteRestore(formData: FormData): Promise<FormState> {
  const kind = formData.get('kind');
  const version = Number(formData.get('version'));
  const publish = formData.get('intent') === 'restorePublish';
  if (!isSiteKind(kind) || !Number.isSafeInteger(version) || version <= 0) return failState(adminMessages.notFound);
  const result = await restoreSiteVersion(kind, version, publish);
  if (!result.ok) return siteProblemState(result.problem);
  refresh(kind);
  if (publish) expireStorefrontConfig();
  const n = formatNumber(version);
  return okState(publish ? siteMessages.messages.restoredPublished(n) : siteMessages.messages.restored(n));
}
