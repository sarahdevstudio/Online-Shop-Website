'use client';

import { useActionState, useState } from 'react';
import { Form, FormActions, FormBlock, FormSection, RequiredNote, TextAreaField } from '@/components/form';
import { INITIAL_FORM_STATE } from '@/lib/admin/form-state';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminTranslation } from '@/lib/admin/types';
import { formatNumber } from '@/lib/format';
import { FormBanner } from '../../_components/FormBits';
import { IntentButton } from '../site/_components/IntentButton';
import { saveTranslationAction } from './actions';

const t = adminMessages.translations;
const MAX = 2000;

/** One text: the shipped text for reference, the placeholders it must keep, and the editable text. */
export function TranslationEditor({ row }: { row: AdminTranslation }) {
  const [state, action] = useActionState(saveTranslationAction, INITIAL_FORM_STATE);
  const [text, setText] = useState(row.text);
  const [version, setVersion] = useState(row.rowVersion);
  // After a save, a reset or a reload following a conflict the server's text replaces what is in the box.
  if (version !== row.rowVersion) {
    setVersion(row.rowVersion);
    setText(row.text);
  }

  return (
    <Form action={action} errors={state.errors} banner={<FormBanner state={state} />}>
      <input type="hidden" name="id" value={row.id} />
      <input type="hidden" name="rowVersion" value={row.rowVersion} />
      <RequiredNote />
      <FormSection card>
        <FormBlock title={t.defaultIs}>
          <p>{row.defaultText}</p>
        </FormBlock>
        <FormBlock title={t.placeholders}>
          {row.placeholders.length === 0 ? (
            <p>{t.noPlaceholders}</p>
          ) : (
            <>
              <p>
                {row.placeholders.map((name) => (
                  <code key={name} dir="ltr">{`{${name}} `}</code>
                ))}
              </p>
              <p className="ui-hint">{t.placeholdersHint}</p>
            </>
          )}
        </FormBlock>
        <TextAreaField
          name="text"
          label={t.newText}
          hint={`${t.plainText} ${t.counter(formatNumber(text.length), formatNumber(MAX))}`}
          value={text}
          onChange={(event) => setText(event.target.value)}
          rows={4}
          maxLength={MAX}
          required
        />
        <FormActions end={row.isEdited ? <IntentButton intent="reset" variant="secondary">{t.reset}</IntentButton> : undefined}>
          <IntentButton intent="save" variant="primary">
            {t.save}
          </IntentButton>
        </FormActions>
      </FormSection>
    </Form>
  );
}
