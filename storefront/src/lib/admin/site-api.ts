import { adminFetch, adminGet, type AdminProblem } from './api';
import { failState, problemState, type FormState } from './form-state';
import { siteMessages } from './site-messages';
import type { AdminSiteDocument, PayloadByKind, SiteAuditPage, SiteKind, SiteOverviewItem, SiteVersionDocument } from './site-types';

/**
 * Server-side client of `/api/v1/admin/site/*` (Site.Manage). Pages read with the `get*` helpers; Server Actions write with
 * the functions below. The browser never sees the API: everything goes through `adminFetch` (session + CSRF).
 */

export const getSiteOverview = () => adminGet<SiteOverviewItem[]>('/admin/site');

export const getSiteDocument = <K extends SiteKind>(kind: K) => adminGet<AdminSiteDocument<PayloadByKind[K]>>(`/admin/site/${kind}`);

export const getSiteVersion = <K extends SiteKind>(kind: K, version: number) =>
  adminGet<SiteVersionDocument<PayloadByKind[K]>>(`/admin/site/${kind}/versions/${version}`);

export function getSiteAudit(kind: SiteKind | null, page: number) {
  const query = new URLSearchParams({ page: String(page) });
  if (kind) query.set('kind', kind);
  return adminGet<SiteAuditPage>(`/admin/site/audit?${query}`);
}

export const saveSiteDraft = <K extends SiteKind>(kind: K, payload: PayloadByKind[K], rowVersion: string | null) =>
  adminFetch<AdminSiteDocument<PayloadByKind[K]>>('PUT', `/admin/site/${kind}/draft`, { body: { payload, rowVersion } });

export const publishSite = (kind: SiteKind, rowVersion: string) => adminFetch<AdminSiteDocument<unknown>>('POST', `/admin/site/${kind}/publish`, { body: { rowVersion } });

export const discardSiteDraft = (kind: SiteKind, rowVersion: string) => adminFetch<AdminSiteDocument<unknown>>('POST', `/admin/site/${kind}/discard`, { body: { rowVersion } });

export const restoreSiteVersion = (kind: SiteKind, version: number, publish: boolean) =>
  adminFetch<AdminSiteDocument<unknown>>('POST', `/admin/site/${kind}/restore/${version}`, { body: { publish } });

/**
 * One place that turns an API problem into form state. Validation keys are payload paths (`blocks[0].data.title`), used as
 * the field names, so the editor shows each message under its field. The several `contrast` messages (publish blocked by an
 * unreadable palette) stay together under one key.
 */
export function siteProblemState(problem: AdminProblem, submitted: Record<string, string> = {}): FormState {
  if (problem.status === 404 && problem.code === 'draft_not_found') return failState(siteMessages.messages.noDraft, submitted);
  const state = problemState(problem, submitted);
  const contrast = problem.errors?.contrast;
  if (contrast && contrast.length > 0) {
    return { ...state, message: siteMessages.messages.publishBlocked, errors: { ...state.errors, contrast: contrast.join('؛ ') } };
  }
  return state;
}
