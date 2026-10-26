'use client';

import { useActionState, useState } from 'react';
import { Alert } from '@/components/Alert';
import { INITIAL_FORM_STATE } from '@/lib/admin/form-state';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminContentPage } from '@/lib/admin/types';
import { renderMarkdown } from '@/lib/markdown';
import { Field, FormBanner, SubmitButton } from '../../_components/FormBits';
import { saveContentAction } from './actions';
import styles from '../../admin.module.css';

const t = adminMessages.content;

/** Title + Markdown textarea with a live preview rendered by the same safe renderer the storefront uses. */
export function ContentEditor({ page }: { page: AdminContentPage }) {
  const [state, action] = useActionState(saveContentAction, INITIAL_FORM_STATE);
  const [body, setBody] = useState(page.body);
  const [version, setVersion] = useState(page.rowVersion);

  // After a save (or a reload following a conflict) the server's text replaces what is in the box.
  if (version !== page.rowVersion) {
    setVersion(page.rowVersion);
    setBody(page.body);
  }

  return (
    <form action={action} className={styles.grid2}>
      <input type="hidden" name="slug" value={page.slug} />
      <input type="hidden" name="rowVersion" value={page.rowVersion} />
      <div className={styles.wide}>
        <FormBanner state={state} />
        {page.isDraft && <Alert variant="warning">{t.draftWarning}</Alert>}
      </div>

      <div key={page.rowVersion} style={{ display: 'contents' }}>
        <Field name="title" label={t.pageTitle} state={state} defaultValue={page.title} required wide />
      </div>

      <label className={styles.field}>
        <span className={styles.fieldLabel}>{t.body}</span>
        <textarea
          className="ui-control"
          name="body"
          rows={22}
          dir="auto"
          value={body}
          onChange={(event) => setBody(event.target.value)}
          aria-invalid={state.errors.body ? true : undefined}
          style={{ fontFamily: 'ui-monospace, monospace' }}
        />
        {state.errors.body && <span className={styles.error}>{state.errors.body}</span>}
      </label>

      <section className={styles.field} aria-label={t.preview}>
        <span className={styles.fieldLabel}>{t.preview}</span>
        {/* Safe: raw HTML is disabled and only safe links survive (lib/markdown.ts), same as on the public page. */}
        <div className={`ui-prose ${styles.panel}`} dangerouslySetInnerHTML={{ __html: renderMarkdown(body) }} />
      </section>

      <div className={styles.row}>
        <SubmitButton>{adminMessages.save}</SubmitButton>
      </div>
    </form>
  );
}
