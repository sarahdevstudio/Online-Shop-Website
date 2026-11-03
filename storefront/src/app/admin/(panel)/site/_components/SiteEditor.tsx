'use client';

import Link from 'next/link';
import { createContext, useActionState, useContext, type ReactNode } from 'react';
import { Badge } from '@/components/Badge';
import { Form, FormActions, RequiredNote } from '@/components/form';
import { INITIAL_FORM_STATE, type FormState } from '@/lib/admin/form-state';
import { siteMessages } from '@/lib/admin/site-messages';
import { formatSiteTime } from '@/lib/admin/site-format';
import type { SiteKind } from '@/lib/admin/site-types';
import { formatNumber } from '@/lib/format';
import { FormBanner } from '../../../_components/FormBits';
import { IntentButton } from './IntentButton';
import styles from './site.module.css';

interface FieldContextValue {
  state: FormState;
  /** What the admin typed when the last save was refused, else the stored value (React resets uncontrolled fields after an action). */
  typed: (name: string, stored: string) => string;
}

const FieldContext = createContext<FieldContextValue>({ state: INITIAL_FORM_STATE, typed: (_name, stored) => stored });

export const useSiteEditor = () => useContext(FieldContext);

export interface SiteEditorProps {
  kind: SiteKind;
  action: (previous: FormState, formData: FormData) => Promise<FormState>;
  published: { version: number; publishedAtUtc: string } | null;
  draft: { rowVersion: string; updatedAtUtc: string } | null;
  children: ReactNode;
}

/**
 * The shared frame of every site editor: state strip (draft / published), one form with the fixed button order
 * (save draft · preview · publish, discard at the far end), result banner, error summary and the 409 conflict banner.
 * The editor's own fields are the children; they are remounted whenever the stored version changes.
 */
export function SiteEditor({ kind, action, published, draft, children }: SiteEditorProps) {
  const [state, formAction] = useActionState(action, INITIAL_FORM_STATE);
  const typedValues = !state.ok && Object.keys(state.submitted).length > 0;
  const typed = (name: string, stored: string) => (typedValues && name in state.submitted ? state.submitted[name] : stored);
  const revision = draft?.rowVersion ?? `published-${published?.version ?? 0}`;

  return (
    <FieldContext.Provider value={{ state, typed }}>
      <Form action={formAction} errors={state.errors} banner={<FormBanner state={state} />}>
        <input type="hidden" name="rowVersion" value={draft?.rowVersion ?? ''} />

        <div className={styles.strip}>
          {published ? (
            <Badge variant="success">{`${siteMessages.state.published} · ${siteMessages.state.version(formatNumber(published.version))}`}</Badge>
          ) : (
            <Badge variant="neutral">{siteMessages.state.never}</Badge>
          )}
          {draft ? <Badge variant="warning">{siteMessages.state.draft}</Badge> : <Badge variant="neutral">{siteMessages.state.noDraft}</Badge>}
          <span className={styles.stripText}>
            {draft ? `${siteMessages.state.draftHint} (${formatSiteTime(draft.updatedAtUtc)})` : published ? `${siteMessages.state.lastChange}: ${formatSiteTime(published.publishedAtUtc)}` : ''}
          </span>
          <Link href="/api/site-preview/stop?next=/admin/site" prefetch={false}>
            {siteMessages.actions.stopPreview}
          </Link>
        </div>

        <RequiredNote />

        <div key={revision} style={{ display: 'contents' }}>
          {children}
        </div>

        <FormActions end={draft ? <IntentButton intent="discard" variant="danger">{siteMessages.actions.discard}</IntentButton> : undefined}>
          <IntentButton intent="save" variant="primary">
            {siteMessages.actions.saveDraft}
          </IntentButton>
          <IntentButton intent="preview">{siteMessages.actions.preview}</IntentButton>
          <IntentButton intent="publish">{siteMessages.actions.publish}</IntentButton>
        </FormActions>
        <input type="hidden" name="kind" value={kind} />
      </Form>
    </FieldContext.Provider>
  );
}

