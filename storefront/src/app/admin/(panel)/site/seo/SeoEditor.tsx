'use client';

import { useState } from 'react';
import { FormGrid, FormSection, TextAreaField, TextField } from '@/components/form';
import { siteMessages } from '@/lib/admin/site-messages';
import type { AdminSiteDocument, SeoPayload } from '@/lib/admin/site-types';
import { formatNumber } from '@/lib/format';
import { ImageUploadField } from '../_components/ImageUploadField';
import { SiteEditor } from '../_components/SiteEditor';
import { seoAction } from './actions';

const t = siteMessages.seo;

function counter(value: string, max: number) {
  return t.counter(formatNumber(value.length), formatNumber(max));
}

function SeoFields({ doc }: { doc: AdminSiteDocument<SeoPayload> }) {
  const payload = (doc.draft?.payload ?? doc.published?.payload) as SeoPayload;
  const [siteName, setSiteName] = useState(payload.siteName);
  const [template, setTemplate] = useState(payload.titleTemplate);
  const [homeTitle, setHomeTitle] = useState(payload.homeTitle);
  const [description, setDescription] = useState(payload.defaultDescription);
  const og = payload.defaultOgMediaId ? doc.media[String(payload.defaultOgMediaId)] : undefined;
  const example = template.includes('%s') ? template.replace('%s', t.titleSample) : '';

  return (
    <>
      <FormSection card title={t.title} hint={t.note}>
        <FormGrid>
          <TextField name="siteName" label={t.siteName} hint={`${t.siteNameHint} ${counter(siteName, 60)}`} value={siteName} onChange={(e) => setSiteName(e.target.value)} maxLength={60} required />
          <TextField
            name="titleTemplate"
            label={t.titleTemplate}
            hint={`${t.titleTemplateHint} ${example ? t.titleExample(example) : ''}`}
            value={template}
            onChange={(e) => setTemplate(e.target.value)}
            maxLength={70}
            required
          />
          <TextField name="homeTitle" label={t.homeTitle} hint={`${t.homeTitleHint} ${counter(homeTitle, 70)}`} value={homeTitle} onChange={(e) => setHomeTitle(e.target.value)} maxLength={70} required />
          <TextAreaField
            name="defaultDescription"
            label={t.defaultDescription}
            hint={`${t.defaultDescriptionHint} ${counter(description, 160)}`}
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            rows={3}
            maxLength={160}
          />
        </FormGrid>
        <ImageUploadField
          name="defaultOgMediaId"
          label={t.ogImage}
          hint={t.ogImageHint}
          purpose="og"
          initial={og && payload.defaultOgMediaId ? { id: payload.defaultOgMediaId, url: og.url, width: og.width, height: og.height } : null}
          removable
        />
      </FormSection>
    </>
  );
}

export function SeoEditor({ doc }: { doc: AdminSiteDocument<SeoPayload> }) {
  return (
    <SiteEditor kind="seo" action={seoAction} published={doc.published} draft={doc.draft}>
      <SeoFields doc={doc} />
    </SiteEditor>
  );
}
