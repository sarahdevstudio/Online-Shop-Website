'use client';

import { FormGrid, FormSection, TextField } from '@/components/form';
import { siteMessages } from '@/lib/admin/site-messages';
import type { AdminSiteDocument, BadgesPayload } from '@/lib/admin/site-types';
import { SiteEditor } from '../_components/SiteEditor';
import { badgesAction } from './actions';

const t = siteMessages.badges;

function BadgesFields({ doc }: { doc: AdminSiteDocument<BadgesPayload> }) {
  const payload = (doc.draft?.payload ?? doc.published?.payload) as BadgesPayload;
  return (
    <>
      <FormSection card title={t.title} hint={t.explanation}>
        <p className="ui-hint">{t.footerNote}</p>
      </FormSection>
      <FormSection card title={t.enamad} hint={t.pairNote}>
        <FormGrid>
          <TextField name="enamad.id" label={t.enamadId} hint={t.enamadIdHint} defaultValue={payload.enamad?.id ?? ''} ltr autoComplete="off" maxLength={20} />
          <TextField name="enamad.code" label={t.enamadCode} hint={t.enamadCodeHint} defaultValue={payload.enamad?.code ?? ''} ltr autoComplete="off" maxLength={64} />
        </FormGrid>
      </FormSection>
      <FormSection card title={t.samandehi} hint={t.pairNote}>
        <FormGrid columns={3}>
          <TextField name="samandehi.id" label={t.samandehiId} hint={t.samandehiIdHint} defaultValue={payload.samandehi?.id ?? ''} ltr autoComplete="off" maxLength={20} />
          <TextField name="samandehi.verifyCode" label={t.samandehiVerify} hint={t.samandehiVerifyHint} defaultValue={payload.samandehi?.verifyCode ?? ''} ltr autoComplete="off" maxLength={64} />
          <TextField name="samandehi.logoCode" label={t.samandehiLogo} hint={t.samandehiLogoHint} defaultValue={payload.samandehi?.logoCode ?? ''} ltr autoComplete="off" maxLength={64} />
        </FormGrid>
      </FormSection>
    </>
  );
}

export function BadgesEditor({ doc }: { doc: AdminSiteDocument<BadgesPayload> }) {
  return (
    <SiteEditor kind="badges" action={badgesAction} published={doc.published} draft={doc.draft}>
      <BadgesFields doc={doc} />
    </SiteEditor>
  );
}
