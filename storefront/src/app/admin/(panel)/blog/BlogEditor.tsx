'use client';

import { useActionState, useState } from 'react';
import { INITIAL_FORM_STATE } from '@/lib/admin/form-state';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminBlogPost } from '@/lib/admin/types';
import { renderMarkdown } from '@/lib/markdown';
import { CheckField, Form, FormActions, FormBlock, FormGrid, FormSection, RequiredNote, SubmitButton, TextAreaField, TextField } from '@/components/form';
import { FormBanner } from '../../_components/FormBits';
import { deleteBlogAction, saveBlogAction } from './actions';

const t = adminMessages.blog;

/** Create (no `post`) or edit a post: title, excerpt, Markdown body with a live preview from the storefront's safe renderer. */
export function BlogEditor({ post }: { post?: AdminBlogPost }) {
  const [state, action] = useActionState(saveBlogAction, INITIAL_FORM_STATE);
  const [body, setBody] = useState(post?.body ?? '');
  const [version, setVersion] = useState(post?.rowVersion);

  // After a save (or a reload following a conflict) the server's text replaces what is in the box.
  if (post && version !== post.rowVersion) {
    setVersion(post.rowVersion);
    setBody(post.body);
  }

  return (
    <Form action={action} errors={state.errors} banner={<FormBanner state={state} />}>
      {post && <input type="hidden" name="id" value={post.id} />}
      {post && <input type="hidden" name="rowVersion" value={post.rowVersion} />}
      <RequiredNote />

      <FormGrid>
        <div key={post?.rowVersion ?? 'new'} style={{ display: 'contents' }}>
          <TextField name="title" label={t.postTitle} defaultValue={post?.title} required full maxLength={150} />
          {!post && <TextField name="slug" label={t.slug} hint={t.slugHint} ltr required maxLength={100} />}
          <TextAreaField name="excerpt" label={t.excerpt} defaultValue={post?.excerpt} required rows={3} maxLength={300} />
        </div>

        <TextAreaField name="body" label={t.body} value={body} onChange={(event) => setBody(event.target.value)} rows={22} code />

        <FormBlock title={t.preview}>
          {/* Safe: raw HTML is disabled and only safe links survive (lib/markdown.ts), same as on the public page. */}
          <div className="ui-prose" dangerouslySetInnerHTML={{ __html: renderMarkdown(body) }} />
        </FormBlock>

        <div key={`publish-${post?.rowVersion ?? 'new'}`} style={{ display: 'contents' }}>
          <CheckField name="publish" label={t.publish} hint={t.publishHint} defaultChecked={post?.isPublished} full />
        </div>
      </FormGrid>

      <FormActions>
        <SubmitButton>{adminMessages.save}</SubmitButton>
      </FormActions>
    </Form>
  );
}

/** Deleting is irreversible, so it sits behind a disclosure with a warning. */
export function DeleteBlogForm({ id, slug }: { id: number; slug: string }) {
  const [state, action] = useActionState(deleteBlogAction, INITIAL_FORM_STATE);
  return (
    <details>
      <summary>{t.deleteTitle}</summary>
      <Form action={action} errors={state.errors} banner={<FormBanner state={state} />}>
        <input type="hidden" name="id" value={id} />
        <input type="hidden" name="slug" value={slug} />
        <FormSection hint={t.deleteWarning}>
          <FormActions>
            <SubmitButton variant="danger">{t.deleteConfirm}</SubmitButton>
          </FormActions>
        </FormSection>
      </Form>
    </details>
  );
}
