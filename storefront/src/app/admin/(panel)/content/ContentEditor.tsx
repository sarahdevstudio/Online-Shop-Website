'use client';

import { useActionState, useState } from 'react';
import { Alert } from '@/components/Alert';
import { INITIAL_FORM_STATE } from '@/lib/admin/form-state';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminContentPage } from '@/lib/admin/types';
import { renderMarkdown } from '@/lib/markdown';
import { Form, FormActions, FormBlock, FormGrid, RequiredNote, SubmitButton, TextAreaField, TextField } from '@/components/form';
import { FormBanner } from '../../_components/FormBits';
import { saveContentAction } from './actions';

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
    <Form
      action={action}
      errors={state.errors}
      banner={
        <>
          <FormBanner state={state} />
          {page.isDraft && <Alert variant="warning">{t.draftWarning}</Alert>}
        </>
      }
    >
      <input type="hidden" name="slug" value={page.slug} />
      <input type="hidden" name="rowVersion" value={page.rowVersion} />
      <RequiredNote />

      <FormGrid>
        <div key={page.rowVersion} style={{ display: 'contents' }}>
          <TextField name="title" label={t.pageTitle} defaultValue={page.title} required full />
        </div>

        <TextAreaField name="body" label={t.body} value={body} onChange={(event) => setBody(event.target.value)} rows={22} code />

        <FormBlock title={t.preview}>
          {/* Safe: raw HTML is disabled and only safe links survive (lib/markdown.ts), same as on the public page. */}
          <div className="ui-prose" dangerouslySetInnerHTML={{ __html: renderMarkdown(body) }} />
        </FormBlock>
      </FormGrid>

      <FormActions>
        <SubmitButton>{adminMessages.save}</SubmitButton>
      </FormActions>
    </Form>
  );
}
