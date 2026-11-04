'use client';

import Link from 'next/link';
import { useState } from 'react';
import { FormBlock, TextField } from '@/components/form';
import { adminMessages } from '@/lib/admin/messages';
import { resultingUrl } from '@/lib/admin/route-form';
import type { RouteTarget } from '@/lib/admin/types';
import { site } from '@/lib/seo';

const t = adminMessages.routes;

interface Props {
  /** The entity's current route (edit). Absent on create. */
  route?: RouteTarget;
  /** The path used when nothing is typed on create, e.g. `/product/<slug>`; shown so the admin knows what an empty field means. */
  defaultPattern?: string;
}

/**
 * The "نشانی (URL)" field of the product, category, info-page and blog forms: the typed path, the resulting full URL as it
 * will appear in the browser, the redirect warning, and the old paths that already lead here. Rendered inside a <FormGrid>.
 * On edit it also posts `currentPath` and `path_rowVersion`, so the action only calls the API when the path was edited.
 */
export function PathField({ route, defaultPattern }: Props) {
  const [typed, setTyped] = useState(route?.path ?? '');
  const fallback = route?.path ?? defaultPattern ?? '';
  const changed = route !== undefined && typed.trim() !== '' && typed.trim() !== route.path;

  return (
    <>
      {route && <input type="hidden" name="currentPath" value={route.path} />}
      {route && <input type="hidden" name="path_rowVersion" value={route.rowVersion} />}
      <TextField
        name="path"
        label={t.field}
        hint={route ? t.fieldHintEdit : t.fieldHintNew}
        value={typed}
        onChange={(event) => setTyped(event.target.value)}
        placeholder={route ? undefined : defaultPattern}
        ltr
        full
        maxLength={200}
        autoComplete="off"
        required={Boolean(route)}
      />
      <FormBlock title={t.resultingUrl}>
        <bdi dir="ltr" aria-live="polite">
          {resultingUrl(site.url, typed, fallback)}
        </bdi>
        {changed && route && (
          <p className="ui-hint">
            <bdi dir="ltr">{t.willRedirect(route.path)}</bdi>
          </p>
        )}
      </FormBlock>
      {route && (
        <FormBlock title={t.oldPaths}>
          {route.oldPaths.length === 0 ? (
            <span>{t.oldPathsNone}</span>
          ) : (
            <ul>
              {route.oldPaths.map((old) => (
                <li key={old.id}>
                  <bdi dir="ltr">{old.path}</bdi>
                </li>
              ))}
            </ul>
          )}
          <Link href="/admin/redirects">{t.manageLink}</Link>
        </FormBlock>
      )}
    </>
  );
}
