'use client';

import { useState } from 'react';
import { FormBlock, FormGrid, FormSection, SelectField, TextField } from '@/components/form';
import { contrastReport, isHexColor, onColor, type ContrastItem } from '@/lib/contrast';
import { siteMessages } from '@/lib/admin/site-messages';
import type { AdminSiteDocument, SiteMediaInfo, ThemePayload } from '@/lib/admin/site-types';
import { formatNumber } from '@/lib/format';
import { FONT_IDS, fontFaceCss, fontStack, RADIUS_IDS } from '@/lib/theme-css';
import { ImageUploadField, type UploadedImage } from '../_components/ImageUploadField';
import { SiteEditor } from '../_components/SiteEditor';
import styles from '../_components/site.module.css';
import { themeAction } from './actions';

const t = siteMessages.theme;
const COLORS = ['primary', 'secondary', 'accent', 'background', 'surface', 'text', 'success', 'warning', 'danger'] as const;
type ColorName = (typeof COLORS)[number];

function imageOf(media: Record<string, SiteMediaInfo>, id: number | undefined): UploadedImage | null {
  const info = id === undefined ? undefined : media[String(id)];
  return id !== undefined && info ? { id, url: info.url, width: info.width, height: info.height } : null;
}

function hasFailures(items: readonly ContrastItem[]) {
  return items.some((item) => !item.ok);
}

export function ThemeEditor({ doc }: { doc: AdminSiteDocument<ThemePayload> }) {
  const payload = (doc.draft?.payload ?? doc.published?.payload) as ThemePayload;
  const [colors, setColors] = useState<Record<ColorName, string>>(() => Object.fromEntries(COLORS.map((name) => [name, payload[name]])) as Record<ColorName, string>);
  const valid = COLORS.every((name) => isHexColor(colors[name]));
  const report = valid ? contrastReport(colors) : null;
  const saved = doc.report?.contrast ?? [];

  return (
    <SiteEditor kind="theme" action={themeAction} published={doc.published} draft={doc.draft}>
      <FormSection card title={t.colors} hint={t.colorsHint}>
        <FormGrid columns={3}>
          {COLORS.map((name) => (
            <TextField
              key={name}
              name={name}
              label={t[name]}
              hint={name === 'accent' ? t.accentHint : undefined}
              value={colors[name]}
              onChange={(event) => setColors((current) => ({ ...current, [name]: event.target.value }))}
              ltr
              maxLength={7}
              autoComplete="off"
              required
            />
          ))}
        </FormGrid>
        <FormBlock title={t.swatches}>
          <ul className={styles.swatches}>
            {COLORS.map((name) => {
              const value = colors[name];
              const ok = isHexColor(value);
              return (
                <li key={name} className={styles.swatch} style={ok ? { background: value, color: onColor(value) } : undefined}>
                  <span>{t[name]}</span>
                  <bdi dir="ltr">{ok ? value.toLowerCase() : '—'}</bdi>
                </li>
              );
            })}
          </ul>
        </FormBlock>
      </FormSection>

      <FormSection card title={t.contrastTitle} hint={t.contrastIntro}>
        {report ? (
          <>
            <p role="status" className={hasFailures(report) ? 'ui-error' : 'ui-hint'}>
              {hasFailures(report) ? t.contrastBad : t.contrastOk}
            </p>
            <ul className={styles.contrast}>
              {report.map((item) => (
                <li key={item.id} className={`${styles.contrastItem} ${item.ok ? '' : styles.contrastBad}`}>
                  <span className={`${styles.chip} ${item.ok ? styles.chipOk : styles.chipBad}`}>{item.ok ? t.contrastPass : t.contrastFail}</span>
                  <span>{item.message}</span>
                </li>
              ))}
            </ul>
          </>
        ) : (
          <p className="ui-hint">{t.contrastInvalid}</p>
        )}
        {doc.draft && hasFailures(saved) && <p className="ui-hint">{t.serverReport(formatNumber(saved.filter((item) => !item.ok).length))}</p>}
      </FormSection>

      <FormSection card title={t.typography}>
        <style dangerouslySetInnerHTML={{ __html: FONT_IDS.map(fontFaceCss).join('') }} />
        <FormGrid>
          <SelectField name="font" label={t.font} hint={t.fontHint} defaultValue={payload.font} required>
            {FONT_IDS.map((id) => (
              <option key={id} value={id}>
                {t.fonts[id]}
              </option>
            ))}
          </SelectField>
          <SelectField name="radius" label={t.radius} defaultValue={payload.radius} required>
            {RADIUS_IDS.map((id) => (
              <option key={id} value={id}>
                {t.radii[id]}
              </option>
            ))}
          </SelectField>
        </FormGrid>
        <FormBlock title={t.fontSamples}>
          <ul className={styles.samples}>
            {FONT_IDS.map((id) => (
              <li key={id} className={styles.fontSample} style={{ fontFamily: fontStack(id) }}>
                <strong>{t.fonts[id]}</strong> — {t.fontSample}
              </li>
            ))}
          </ul>
        </FormBlock>
      </FormSection>

      <FormSection card title={t.brand}>
        <ImageUploadField name="logo.mediaId" label={t.logo} hint={t.logoHint} purpose="logo" initial={imageOf(doc.media, payload.logo?.mediaId)} removable onPrimary />
        <TextField name="logo.alt" label={t.logoAlt} defaultValue={payload.logo?.alt ?? ''} maxLength={100} />
        <ImageUploadField name="favicon.mediaId" label={t.favicon} hint={t.faviconHint} purpose="favicon" initial={imageOf(doc.media, payload.favicon?.mediaId)} removable onPrimary />
      </FormSection>
    </SiteEditor>
  );
}
