'use client';

import { useActionState, useState } from 'react';
import { INITIAL_FORM_STATE } from '@/lib/admin/form-state';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminBlogPost } from '@/lib/admin/types';
import { renderMarkdown } from '@/lib/markdown';
import { CheckField, Field, FormBanner, SubmitButton } from '../../_components/FormBits';
import { deleteBlogAction, saveBlogAction } from './actions';
import styles from '../../admin.module.css';

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
    <form action={action} className={styles.grid2}>
      {post && <input type="hidden" name="id" value={post.id} />}
      {post && <input type="hidden" name="rowVersion" value={post.rowVersion} />}
      <div className={styles.wide}>
        <FormBanner state={state} />
      </div>

      <div key={post?.rowVersion ?? 'new'} style={{ display: 'contents' }}>
        <Field name="title" label={t.postTitle} state={state} defaultValue={post?.title} required wide maxLength={150} />
        {!post && <Field name="slug" label={t.slug} state={state} hint={t.slugHint} ltr required maxLength={100} />}
        <Field name="excerpt" label={t.excerpt} state={state} defaultValue={post?.excerpt} required wide multiline maxLength={300} />
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

      <div className={styles.wide}>
        <div key={post?.rowVersion ?? 'new-publish'}>
          <CheckField name="publish" label={t.publish} defaultChecked={post?.isPublished} />
        </div>
        <p className={styles.hint}>{t.publishHint}</p>
      </div>

      <div className={styles.row}>
        <SubmitButton>{adminMessages.save}</SubmitButton>
      </div>
    </form>
  );
}

/** Deleting is irreversible, so it sits behind a disclosure with a warning. */
export function DeleteBlogForm({ id, slug }: { id: number; slug: string }) {
  const [state, action] = useActionState(deleteBlogAction, INITIAL_FORM_STATE);
  return (
    <details className={styles.panel}>
      <summary>{t.deleteTitle}</summary>
      <form action={action}>
        <input type="hidden" name="id" value={id} />
        <input type="hidden" name="slug" value={slug} />
        <FormBanner state={state} />
        <p className={styles.hint}>{t.deleteWarning}</p>
        <SubmitButton variant="danger">{t.deleteConfirm}</SubmitButton>
      </form>
    </details>
  );
}
