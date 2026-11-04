'use client';

import { useActionState } from 'react';
import { Form, FormActions, FormSection, SubmitButton } from '@/components/form';
import { INITIAL_FORM_STATE } from '@/lib/admin/form-state';
import { adminMessages } from '@/lib/admin/messages';
import { FormBanner } from '../../_components/FormBits';
import { releaseRedirectAction } from './actions';

const t = adminMessages.routes;

/** Releasing is irreversible (visitors with the old link get "not found"), so it sits behind a disclosure with a warning. */
export function ReleaseForm({ id, path, back }: { id: number; path: string; back: string }) {
  const [state, action] = useActionState(releaseRedirectAction, INITIAL_FORM_STATE);
  return (
    <details>
      <summary>
        {t.release} <span className="ui-visually-hidden">{path}</span>
      </summary>
      <Form action={action} errors={state.errors} banner={<FormBanner state={state} />}>
        <input type="hidden" name="id" value={id} />
        <input type="hidden" name="back" value={back} />
        <FormSection hint={t.releaseWarning}>
          <FormActions>
            <SubmitButton variant="danger">{t.releaseConfirm}</SubmitButton>
          </FormActions>
        </FormSection>
      </Form>
    </details>
  );
}
