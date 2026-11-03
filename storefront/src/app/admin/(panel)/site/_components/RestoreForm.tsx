'use client';

import { useActionState } from 'react';
import { Alert } from '@/components/Alert';
import { Form } from '@/components/form';
import { INITIAL_FORM_STATE } from '@/lib/admin/form-state';
import { siteMessages } from '@/lib/admin/site-messages';
import type { SiteKind } from '@/lib/admin/site-types';
import { restoreSiteAction } from '../actions';
import { IntentButton } from './IntentButton';
import styles from './site.module.css';

/** «بازگردانی» (into the draft) and «بازگردانی و انتشار» for one archived version. */
export function RestoreForm({ kind, version }: { kind: SiteKind; version: number }) {
  const [state, action] = useActionState(restoreSiteAction, INITIAL_FORM_STATE);
  return (
    <Form action={action} errors={state.errors} inline>
      <input type="hidden" name="kind" value={kind} />
      <input type="hidden" name="version" value={version} />
      <div className={styles.rowActions}>
        <IntentButton intent="restore">{siteMessages.actions.restore}</IntentButton>
        <IntentButton intent="restorePublish">{siteMessages.actions.restorePublish}</IntentButton>
      </div>
      {state.message && <Alert variant={state.ok ? 'success' : 'danger'}>{state.message}</Alert>}
    </Form>
  );
}
