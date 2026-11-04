'use client';

import { useState } from 'react';
import { Button } from '@/components/Button';
import { CheckField, FormGrid, FormSection, SelectField, TextAreaField, TextField } from '@/components/form';
import { useSiteEditor } from '../_components/SiteEditor';
import {
  AUTOPLAY_OPTIONS,
  BANNER_HEIGHTS,
  BLOCK_TYPES,
  HOME_LIMITS,
  ROW_LIMIT_OPTIONS,
  ROW_SOURCES,
  blockErrorCount,
  canAddBlock,
  fromPayload,
  moveItem,
  newBlock,
  newSlide,
  removeAt,
  replaceAt,
  toPayload,
  type BannerHeight,
  type BlockState,
  type BlockType,
  type RowSource,
} from '@/lib/admin/site-home-model';
import { siteEditorMessages } from '@/lib/admin/site-messages';
import type { AdminSiteDocument, HomePayload, SiteMediaInfo } from '@/lib/admin/site-types';
import { formatNumber } from '@/lib/format';
import { LinkField } from '../_components/LinkField';
import { ImageUploadField, type UploadedImage } from '../_components/ImageUploadField';
import { SiteEditor } from '../_components/SiteEditor';
import styles from '../_components/site.module.css';
import { homeAction } from './actions';

const t = siteEditorMessages.home;
type Category = { id: number; name: string };

function imageOf(media: Record<string, SiteMediaInfo>, id: number | null): UploadedImage | null {
  const info = id === null ? undefined : media[String(id)];
  return id !== null && info ? { id, url: info.url, width: info.width, height: info.height } : null;
}

function HomeFields({ doc, categories }: { doc: AdminSiteDocument<HomePayload>; categories: Category[] }) {
  const [blocks, setBlocks] = useState<BlockState[]>(() => fromPayload(doc.draft?.payload ?? doc.published?.payload));
  const [newType, setNewType] = useState<BlockType>('hero');
  const { state } = useSiteEditor();
  const media = doc.media;

  const patch = (index: number, change: (block: BlockState) => BlockState) => setBlocks((current) => replaceAt(current, index, change(current[index])));

  return (
    <>
      <input type="hidden" name="payload" value={JSON.stringify(toPayload(blocks))} />
      <FormSection card title={t.blocks} hint={t.intro}>
        {blocks.length === 0 && <p className="ui-hint">{t.empty}</p>}
        <ol className={styles.blockList}>
          {blocks.map((block, index) => {
            const errors = blockErrorCount(state.errors, index);
            const n = formatNumber(index + 1);
            return (
              <li key={block.id} className={styles.blockCard}>
                <div className={styles.blockHead}>
                  <h3 className="ui-heading-4">{t.cardTitle(n, t.types[block.type])}</h3>
                  {errors > 0 && <span className="ui-error">{t.withErrors(formatNumber(errors))}</span>}
                  <div className={styles.blockTools}>
                    <Button type="button" variant="secondary" disabled={index === 0} onClick={() => setBlocks(moveItem(blocks, index, -1))}>
                      {t.moveUp}
                    </Button>
                    <Button type="button" variant="secondary" disabled={index === blocks.length - 1} onClick={() => setBlocks(moveItem(blocks, index, 1))}>
                      {t.moveDown}
                    </Button>
                    <Button type="button" variant="secondary" onClick={() => setBlocks(removeAt(blocks, index))}>
                      {t.remove}
                    </Button>
                  </div>
                </div>
                <CheckField name={`blocks[${index}].enabled`} label={t.enabled} checked={block.enabled} onChange={(e) => patch(index, (b) => ({ ...b, enabled: e.target.checked }))} />
                <BlockFields block={block} index={index} categories={categories} media={media} known={doc.links} onChange={(next) => patch(index, () => next)} />
              </li>
            );
          })}
        </ol>
        <FormGrid>
          <SelectField name="newBlockType" label={t.addType} value={newType} onChange={(e) => setNewType(e.target.value as BlockType)}>
            {BLOCK_TYPES.map((type) => (
              <option key={type} value={type}>
                {t.types[type]}
              </option>
            ))}
          </SelectField>
          <div className={styles.addRow}>
            <Button type="button" variant="secondary" disabled={!canAddBlock(blocks)} onClick={() => setBlocks([...blocks, newBlock(newType, crypto.randomUUID())])}>
              {t.add}
            </Button>
            {!canAddBlock(blocks) && <span className="ui-hint">{t.limit(formatNumber(HOME_LIMITS.blocks))}</span>}
          </div>
        </FormGrid>
      </FormSection>
    </>
  );
}

function BlockFields({ block, index, categories, media, known, onChange }: { block: BlockState; index: number; categories: Category[]; media: Record<string, SiteMediaInfo>; known: AdminSiteDocument<HomePayload>['links']; onChange: (next: BlockState) => void }) {
  const base = `blocks[${index}].data`;
  switch (block.type) {
    case 'hero': {
      const { data } = block;
      const set = (next: typeof data) => onChange({ ...block, data: next });
      return (
        <>
          <SelectField name={`${base}.autoplaySeconds`} label={t.heroAutoplay} value={data.autoplaySeconds} onChange={(e) => set({ ...data, autoplaySeconds: Number(e.target.value) })}>
            {AUTOPLAY_OPTIONS.map((s) => (
              <option key={s} value={s}>
                {s === 0 ? t.heroAutoplayOff : t.heroAutoplaySec(formatNumber(s))}
              </option>
            ))}
          </SelectField>
          {data.slides.map((slide, i) => {
            const sp = `${base}.slides[${i}]`;
            const setSlide = (change: Partial<typeof slide>) => set({ ...data, slides: replaceAt(data.slides, i, { ...slide, ...change }) });
            return (
              <FormSection key={i} card title={t.slide(formatNumber(i + 1))}>
                <ImageUploadField name={`${sp}.mediaId`} label={t.slideImage} purpose="hero" initial={imageOf(media, slide.mediaId)} removable onChange={(img) => setSlide({ mediaId: img?.id ?? null })} />
                <FormGrid>
                  <TextField name={`${sp}.alt`} label={t.slideAlt} value={slide.alt} onChange={(e) => setSlide({ alt: e.target.value })} maxLength={HOME_LIMITS.alt} />
                  <TextField name={`${sp}.title`} label={t.slideTitle} value={slide.title} onChange={(e) => setSlide({ title: e.target.value })} maxLength={HOME_LIMITS.title} />
                  <TextField name={`${sp}.text`} label={t.slideText} value={slide.text} onChange={(e) => setSlide({ text: e.target.value })} maxLength={HOME_LIMITS.slideText} full />
                  <TextField name={`${sp}.linkLabel`} label={t.slideLinkLabel} value={slide.linkLabel} onChange={(e) => setSlide({ linkLabel: e.target.value })} maxLength={HOME_LIMITS.linkLabel} />
                  <LinkField name={`${sp}.link`} label={t.slideLink} value={{ link: slide.link || null, entity: slide.entity }} known={known} onChange={(v) => setSlide({ link: v.link ?? '', entity: v.entity })} />
                </FormGrid>
                {data.slides.length > HOME_LIMITS.slides.min && (
                  <Button type="button" variant="secondary" onClick={() => set({ ...data, slides: removeAt(data.slides, i) })}>
                    {t.remove}
                  </Button>
                )}
              </FormSection>
            );
          })}
          {data.slides.length < HOME_LIMITS.slides.max && (
            <Button type="button" variant="secondary" onClick={() => set({ ...data, slides: [...data.slides, newSlide()] })}>
              {t.addSlide}
            </Button>
          )}
        </>
      );
    }
    case 'categoryTiles': {
      const { data } = block;
      const set = (items: typeof data.items) => onChange({ ...block, data: { items } });
      return (
        <>
          {data.items.map((tile, i) => (
            <FormGrid key={i}>
              <SelectField name={`${base}.items[${i}].categoryId`} label={`${t.tile(formatNumber(i + 1))} — ${t.tileCategory}`} value={tile.categoryId || ''} onChange={(e) => set(replaceAt(data.items, i, { ...tile, categoryId: Number(e.target.value) }))}>
                <option value="">{t.pickCategory}</option>
                {categories.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </SelectField>
              <TextField name={`${base}.items[${i}].label`} label={t.tileLabel} value={tile.label} onChange={(e) => set(replaceAt(data.items, i, { ...tile, label: e.target.value }))} maxLength={HOME_LIMITS.tileLabel} />
              <Button type="button" variant="secondary" onClick={() => set(removeAt(data.items, i))}>
                {t.remove}
              </Button>
            </FormGrid>
          ))}
          {data.items.length < HOME_LIMITS.tiles.max && (
            <Button type="button" variant="secondary" onClick={() => set([...data.items, { categoryId: 0, label: '' }])}>
              {t.addTile}
            </Button>
          )}
        </>
      );
    }
    case 'productRow': {
      const { data } = block;
      const set = (change: Partial<typeof data>) => onChange({ ...block, data: { ...data, ...change } });
      return (
        <FormGrid>
          <TextField name={`${base}.title`} label={t.rowTitle} value={data.title} onChange={(e) => set({ title: e.target.value })} maxLength={HOME_LIMITS.rowTitle} required />
          <SelectField name={`${base}.source`} label={t.rowSource} value={data.source} onChange={(e) => set({ source: e.target.value as RowSource })}>
            {ROW_SOURCES.map((s) => (
              <option key={s} value={s}>
                {t.sources[s]}
              </option>
            ))}
          </SelectField>
          {data.source === 'category' && (
            <SelectField name={`${base}.categoryId`} label={t.rowCategory} value={data.categoryId ?? ''} onChange={(e) => set({ categoryId: e.target.value ? Number(e.target.value) : null })}>
              <option value="">{t.pickCategory}</option>
              {categories.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </SelectField>
          )}
          {data.source === 'handpicked' && (
            <TextField
              name={`${base}.productIds`}
              label={t.rowIds}
              hint={t.rowIdsHint}
              value={data.productIds.join(', ')}
              onChange={(e) => set({ productIds: e.target.value.split(/[\s,،]+/).filter(Boolean).map(Number).filter((n) => Number.isSafeInteger(n) && n > 0) })}
              ltr
              autoComplete="off"
              full
            />
          )}
          <SelectField name={`${base}.limit`} label={t.rowLimit} value={data.limit} onChange={(e) => set({ limit: Number(e.target.value) })}>
            {ROW_LIMIT_OPTIONS.map((n) => (
              <option key={n} value={n}>
                {formatNumber(n)}
              </option>
            ))}
          </SelectField>
        </FormGrid>
      );
    }
    case 'text': {
      const { data } = block;
      return (
        <FormGrid>
          <TextField name={`${base}.heading`} label={t.textHeading} value={data.heading} onChange={(e) => onChange({ ...block, data: { ...data, heading: e.target.value } })} maxLength={HOME_LIMITS.title} full />
          <TextAreaField name={`${base}.body`} label={t.textBody} hint={t.textBodyHint} value={data.body} onChange={(e) => onChange({ ...block, data: { ...data, body: e.target.value } })} rows={6} maxLength={HOME_LIMITS.textBody} />
        </FormGrid>
      );
    }
    case 'banner': {
      const { data } = block;
      const set = (change: Partial<typeof data>) => onChange({ ...block, data: { ...data, ...change } });
      return (
        <>
          <ImageUploadField name={`${base}.mediaId`} label={t.bannerImage} purpose="banner" initial={imageOf(media, data.mediaId)} onChange={(img) => set({ mediaId: img?.id ?? null })} />
          <FormGrid>
            <TextField name={`${base}.alt`} label={t.bannerAlt} value={data.alt} onChange={(e) => set({ alt: e.target.value })} maxLength={HOME_LIMITS.alt} />
            <LinkField name={`${base}.link`} label={t.bannerLink} value={{ link: data.link || null, entity: data.entity }} known={known} onChange={(v) => set({ link: v.link ?? '', entity: v.entity })} />
            <SelectField name={`${base}.height`} label={t.bannerHeight} value={data.height} onChange={(e) => set({ height: e.target.value as BannerHeight })}>
              {BANNER_HEIGHTS.map((h) => (
                <option key={h} value={h}>
                  {t.heights[h]}
                </option>
              ))}
            </SelectField>
          </FormGrid>
        </>
      );
    }
  }
}

export function HomeEditor({ doc, categories }: { doc: AdminSiteDocument<HomePayload>; categories: Category[] }) {
  return (
    <SiteEditor kind="home" action={homeAction} published={doc.published} draft={doc.draft}>
      <HomeFields doc={doc} categories={categories} />
    </SiteEditor>
  );
}
