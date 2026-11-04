'use client';

import { useState } from 'react';
import { Button } from '@/components/Button';
import { CheckField, FormBlock, FormGrid, FormSection, SelectField, TextAreaField, TextField } from '@/components/form';
import {
  ANNOUNCEMENT_STYLES,
  CHROME_LIMITS,
  SOCIAL_PLATFORMS,
  newFooterColumn,
  newFooterLink,
  newMenuChild,
  newMenuItem,
  normalizeChrome,
  socialForSave,
  type AnnouncementStyle,
  type ChromePayload,
  type SocialPlatform,
} from '@/lib/admin/site-chrome-model';
import { optionalLink, requiredLink } from '@/lib/admin/link-model';
import { moveItem, removeAt, replaceAt } from '@/lib/admin/site-home-model';
import { siteEditorMessages } from '@/lib/admin/site-messages';
import type { AdminSiteDocument } from '@/lib/admin/site-types';
import { formatNumber } from '@/lib/format';
import { SiteEditor } from '../_components/SiteEditor';
import styles from '../_components/site.module.css';
import { LinkField } from '../_components/LinkField';
import { chromeAction } from './actions';

const t = siteEditorMessages.chrome;

function Tools({ index, count, onMove, onRemove }: { index: number; count: number; onMove: (delta: number) => void; onRemove: () => void }) {
  return (
    <div className={styles.blockTools}>
      <Button type="button" variant="secondary" disabled={index === 0} onClick={() => onMove(-1)}>
        {t.moveUp}
      </Button>
      <Button type="button" variant="secondary" disabled={index === count - 1} onClick={() => onMove(1)}>
        {t.moveDown}
      </Button>
      <Button type="button" variant="secondary" onClick={onRemove}>
        {t.remove}
      </Button>
    </div>
  );
}

function ChromeFields({ doc }: { doc: AdminSiteDocument<ChromePayload> }) {
  const initial = normalizeChrome(doc.draft?.payload ?? doc.published?.payload);
  const known = doc.links;
  const [ann, setAnn] = useState(initial.announcement);
  const [menu, setMenu] = useState(initial.headerMenu);
  const [about, setAbout] = useState(initial.footer.about);
  const [columns, setColumns] = useState(initial.footer.columns);
  const [social, setSocial] = useState<Record<SocialPlatform, string>>(() => Object.fromEntries(SOCIAL_PLATFORMS.map((p) => [p, initial.social[p] ?? ''])) as Record<SocialPlatform, string>);

  const payload: ChromePayload = {
    announcement: { ...ann, ...optionalLink({ link: ann.link, entity: ann.entity ?? null }) },
    headerMenu: menu.map((m) => ({
      ...m,
      ...optionalLink({ link: m.link, entity: m.entity ?? null }),
      children: m.children.map((c) => ({ ...c, ...requiredLink({ link: c.link, entity: c.entity ?? null }) })),
    })),
    footer: { about, columns: columns.map((c) => ({ ...c, links: c.links.map((l) => ({ ...l, ...requiredLink({ link: l.link, entity: l.entity ?? null }) })) })) },
    social: socialForSave(social),
  };

  return (
    <>
      <input type="hidden" name="payload" value={JSON.stringify(payload)} />

      <FormSection card title={t.announcement}>
        <CheckField name="announcement.enabled" label={t.annEnabled} checked={ann.enabled} onChange={(e) => setAnn({ ...ann, enabled: e.target.checked })} />
        <FormGrid>
          <TextField name="announcement.text" label={t.annText} value={ann.text} onChange={(e) => setAnn({ ...ann, text: e.target.value })} maxLength={CHROME_LIMITS.announcementText} full />
          <LinkField name="announcement.link" label={t.annLink} value={{ link: ann.link, entity: ann.entity ?? null }} known={known} onChange={(v) => setAnn({ ...ann, ...v })} />
          <SelectField name="announcement.style" label={t.annStyle} value={ann.style} onChange={(e) => setAnn({ ...ann, style: e.target.value as AnnouncementStyle })}>
            {ANNOUNCEMENT_STYLES.map((s) => (
              <option key={s} value={s}>
                {t.styles[s]}
              </option>
            ))}
          </SelectField>
        </FormGrid>
        <CheckField name="announcement.dismissible" label={t.annDismissible} checked={ann.dismissible} onChange={(e) => setAnn({ ...ann, dismissible: e.target.checked })} />
      </FormSection>

      <FormSection card title={t.menu} hint={t.menuIntro}>
        <ol className={styles.blockList}>
          {menu.map((item, i) => {
            const setItem = (next: typeof item) => setMenu(replaceAt(menu, i, next));
            return (
              <li key={i} className={styles.blockCard}>
                <div className={styles.blockHead}>
                  <h3 className="ui-heading-4">{t.menuItem(formatNumber(i + 1))}</h3>
                  <Tools index={i} count={menu.length} onMove={(d) => setMenu(moveItem(menu, i, d))} onRemove={() => setMenu(removeAt(menu, i))} />
                </div>
                <FormGrid>
                  <TextField name={`headerMenu[${i}].label`} label={t.label} value={item.label} onChange={(e) => setItem({ ...item, label: e.target.value })} maxLength={CHROME_LIMITS.menuLabel} required />
                  <LinkField name={`headerMenu[${i}].link`} label={t.link} value={{ link: item.link, entity: item.entity ?? null }} known={known} onChange={(v) => setItem({ ...item, ...v })} />
                </FormGrid>
                <CheckField name={`headerMenu[${i}].allCategories`} label={t.allCategories} checked={item.allCategories} onChange={(e) => setItem({ ...item, allCategories: e.target.checked })} />
                {item.children.map((child, j) => (
                  <FormGrid key={j}>
                    <TextField name={`headerMenu[${i}].children[${j}].label`} label={`${t.child(formatNumber(j + 1))} — ${t.label}`} value={child.label} onChange={(e) => setItem({ ...item, children: replaceAt(item.children, j, { ...child, label: e.target.value }) })} maxLength={CHROME_LIMITS.menuLabel} />
                    <LinkField name={`headerMenu[${i}].children[${j}].link`} label={t.link} value={{ link: child.link, entity: child.entity ?? null }} known={known} onChange={(v) => setItem({ ...item, children: replaceAt(item.children, j, { ...child, link: v.link ?? '', entity: v.entity }) })} />
                    <Button type="button" variant="secondary" onClick={() => setItem({ ...item, children: removeAt(item.children, j) })}>
                      {t.remove}
                    </Button>
                  </FormGrid>
                ))}
                {item.children.length < CHROME_LIMITS.menuChildren && (
                  <Button type="button" variant="secondary" onClick={() => setItem({ ...item, children: [...item.children, newMenuChild()] })}>
                    {t.addChild}
                  </Button>
                )}
              </li>
            );
          })}
        </ol>
        {menu.length < CHROME_LIMITS.menuItems && (
          <Button type="button" variant="secondary" onClick={() => setMenu([...menu, newMenuItem()])}>
            {t.addItem}
          </Button>
        )}
      </FormSection>

      <FormSection card title={t.footer}>
        <TextAreaField name="footer.about" label={t.about} value={about} onChange={(e) => setAbout(e.target.value)} rows={3} maxLength={CHROME_LIMITS.footerAbout} />
        <ol className={styles.blockList}>
          {columns.map((column, i) => {
            const setColumn = (next: typeof column) => setColumns(replaceAt(columns, i, next));
            return (
              <li key={i} className={styles.blockCard}>
                <div className={styles.blockHead}>
                  <h3 className="ui-heading-4">{t.column(formatNumber(i + 1))}</h3>
                  <Tools index={i} count={columns.length} onMove={(d) => setColumns(moveItem(columns, i, d))} onRemove={() => setColumns(removeAt(columns, i))} />
                </div>
                <TextField name={`footer.columns[${i}].title`} label={t.columnTitle} value={column.title} onChange={(e) => setColumn({ ...column, title: e.target.value })} maxLength={CHROME_LIMITS.footerTitle} required />
                {column.links.map((link, j) => (
                  <FormGrid key={j}>
                    <TextField name={`footer.columns[${i}].links[${j}].label`} label={`${t.footerLink(formatNumber(j + 1))} — ${t.label}`} value={link.label} onChange={(e) => setColumn({ ...column, links: replaceAt(column.links, j, { ...link, label: e.target.value }) })} maxLength={CHROME_LIMITS.menuLabel} />
                    <LinkField name={`footer.columns[${i}].links[${j}].link`} label={t.link} value={{ link: link.link, entity: link.entity ?? null }} known={known} onChange={(v) => setColumn({ ...column, links: replaceAt(column.links, j, { ...link, link: v.link ?? '', entity: v.entity }) })} />
                    <Button type="button" variant="secondary" onClick={() => setColumn({ ...column, links: removeAt(column.links, j) })}>
                      {t.remove}
                    </Button>
                  </FormGrid>
                ))}
                {column.links.length < CHROME_LIMITS.footerLinks && (
                  <Button type="button" variant="secondary" onClick={() => setColumn({ ...column, links: [...column.links, newFooterLink()] })}>
                    {t.addLink}
                  </Button>
                )}
              </li>
            );
          })}
        </ol>
        {columns.length < CHROME_LIMITS.footerColumns && (
          <Button type="button" variant="secondary" onClick={() => setColumns([...columns, newFooterColumn()])}>
            {t.addColumn}
          </Button>
        )}
      </FormSection>

      <FormSection card title={t.social} hint={t.socialHint}>
        <FormGrid>
          {SOCIAL_PLATFORMS.map((p) => (
            <TextField key={p} name={`social.${p}`} label={t.platforms[p]} value={social[p]} onChange={(e) => setSocial({ ...social, [p]: e.target.value })} ltr autoComplete="off" />
          ))}
        </FormGrid>
        <FormBlock title="Instagram">
          <p className="ui-hint">{t.instagramNote}</p>
        </FormBlock>
      </FormSection>
    </>
  );
}

export function ChromeEditor({ doc }: { doc: AdminSiteDocument<ChromePayload> }) {
  return (
    <SiteEditor kind="chrome" action={chromeAction} published={doc.published} draft={doc.draft}>
      <ChromeFields doc={doc} />
    </SiteEditor>
  );
}
