'use client';

import { useActionState, useState } from 'react';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { CheckField, Form, FormActions, FormGrid, FormSection, SubmitButton, TextAreaField, TextField } from '@/components/form';
import { INITIAL_FORM_STATE } from '@/lib/admin/form-state';
import { seoMessages as t } from '@/lib/admin/seo-messages';
import { DESCRIPTION_IDEAL, TITLE_IDEAL, type AdminSeo } from '@/lib/admin/seo-types';
import { formatNumber } from '@/lib/format';
import { ImageUploadField } from '../(panel)/site/_components/ImageUploadField';
import { FormBanner } from './FormBits';
import { saveSeoAction } from './seo-actions';

/** Live length hint: plain while within the ideal, a warning once past it. */
function counter(value: string, ideal: number) {
  return value.length > ideal ? t.counterLong(formatNumber(value.length), formatNumber(ideal)) : t.counter(formatNumber(value.length), formatNumber(ideal));
}

/**
 * The SEO fields of one entity (meta title and description with live length hints, share image, noindex with its warning) and,
 * where the entity supports them, the FAQ and the category buying guide. Placed inside the entity's own edit screen; its own form,
 * its own save, so it never touches the entity's data.
 */
export function SeoPanel({ data, id: panelId = 'seo' }: { data: AdminSeo; id?: string }) {
  const [state, action] = useActionState(saveSeoAction, INITIAL_FORM_STATE);
  const [title, setTitle] = useState(data.seo.metaTitle ?? '');
  const [description, setDescription] = useState(data.seo.metaDescription ?? '');
  const [intro, setIntro] = useState(data.intro ?? '');
  const [noIndex, setNoIndex] = useState(data.seo.noIndex);
  const [faq, setFaq] = useState(data.faq.length > 0 ? data.faq : []);

  const update = (index: number, field: 'question' | 'answer', value: string) => setFaq((rows) => rows.map((row, i) => (i === index ? { ...row, [field]: value } : row)));

  return (
    <Form action={action} errors={state.errors} banner={<FormBanner state={state} />}>
      <input type="hidden" name="type" value={data.type} />
      <input type="hidden" name="id" value={data.id} />
      <input type="hidden" name="rowVersion" value={data.rowVersion ?? ''} />
      <FormSection card title={t.title} hint={t.hint}>
        <FormGrid>
          <TextField name="metaTitle" label={t.metaTitle} hint={`${t.metaTitleHint} ${counter(title, TITLE_IDEAL)}`} value={title} onChange={(e) => setTitle(e.target.value)} maxLength={120} autoComplete="off" />
          <TextAreaField
            name="metaDescription"
            label={t.metaDescription}
            hint={`${t.metaDescriptionHint} ${counter(description, DESCRIPTION_IDEAL)}`}
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            rows={3}
            maxLength={320}
          />
        </FormGrid>
        <ImageUploadField
          name="ogMediaId"
          label={t.ogImage}
          hint={t.ogImageHint}
          purpose="og"
          initial={data.seo.ogMediaId && data.seo.ogImageUrl ? { id: data.seo.ogMediaId, url: data.seo.ogImageUrl, width: 1200, height: 630 } : null}
          removable
        />
        <CheckField name="noIndex" label={t.noindex} checked={noIndex} onChange={(e) => setNoIndex(e.target.checked)} full />
        {noIndex && <Alert variant="warning">{t.noindexWarning}</Alert>}
      </FormSection>

      {data.supportsIntro && (
        <FormSection card title={t.introTitle}>
          <TextAreaField name="intro" label={t.intro} hint={`${t.introHint} ${formatNumber(intro.length)} / ${formatNumber(20000)}`} value={intro} onChange={(e) => setIntro(e.target.value)} rows={8} maxLength={20000} />
        </FormSection>
      )}

      {data.supportsFaq && (
        <FormSection card title={t.faqTitle} hint={t.faqHint}>
          {faq.map((row, index) => (
            <FormGrid key={index}>
              <TextField name="faqQuestion" label={`${t.faqQuestion} (${t.faqRow(formatNumber(index + 1))})`} value={row.question} onChange={(e) => update(index, 'question', e.target.value)} maxLength={200} autoComplete="off" error={state.errors[`faq[${index}].question`]} />
              <TextAreaField name="faqAnswer" label={t.faqAnswer} value={row.answer} onChange={(e) => update(index, 'answer', e.target.value)} rows={3} maxLength={2000} error={state.errors[`faq[${index}].answer`]} />
              <Button type="button" variant="secondary" size="sm" onClick={() => setFaq((rows) => rows.filter((_, i) => i !== index))}>
                {t.faqRemove}
              </Button>
            </FormGrid>
          ))}
          {faq.length < 20 && (
            <Button type="button" variant="secondary" onClick={() => setFaq((rows) => [...rows, { question: '', answer: '' }])}>
              {t.faqAdd}
            </Button>
          )}
        </FormSection>
      )}

      <FormActions>
        <SubmitButton>{t.save}</SubmitButton>
      </FormActions>
    </Form>
  );
}
