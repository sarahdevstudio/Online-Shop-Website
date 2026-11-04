import { optionalLink } from './link-model';
import type { HomeBlockPayload, HomePayload, LinkEntity } from './site-types';

/**
 * Pure model of the home page editor: the editor works on string-typed state (a text input is never `null`), converted
 * from and to the API payload at the edges. Limits mirror the server's validation (docs/10 §5); the server stays the
 * authority, these only keep the editor usable.
 */

export const BLOCK_TYPES = ['hero', 'categoryTiles', 'productRow', 'text', 'banner'] as const;
export type BlockType = (typeof BLOCK_TYPES)[number];

export const HOME_LIMITS = {
  blocks: 20,
  slides: { min: 1, max: 6 },
  tiles: { min: 2, max: 12 },
  handpicked: { min: 1, max: 12 },
  rowLimit: { min: 4, max: 12 },
  title: 80,
  slideText: 200,
  linkLabel: 30,
  alt: 150,
  rowTitle: 60,
  tileLabel: 40,
  textBody: 3000,
} as const;

export const AUTOPLAY_OPTIONS = [0, 4, 5, 6, 7, 8, 9, 10, 11, 12] as const;
export const ROW_SOURCES = ['newest', 'discounted', 'category', 'handpicked'] as const;
export type RowSource = (typeof ROW_SOURCES)[number];
export const BANNER_HEIGHTS = ['sm', 'md', 'lg'] as const;
export type BannerHeight = (typeof BANNER_HEIGHTS)[number];
export const ROW_LIMIT_OPTIONS = Array.from({ length: HOME_LIMITS.rowLimit.max - HOME_LIMITS.rowLimit.min + 1 }, (_, i) => HOME_LIMITS.rowLimit.min + i);

export interface SlideState {
  mediaId: number | null;
  alt: string;
  title: string;
  text: string;
  linkLabel: string;
  link: string;
  entity: LinkEntity | null;
}
export interface HeroState {
  autoplaySeconds: number;
  slides: SlideState[];
}
export interface TileState {
  categoryId: number;
  label: string;
}
export interface CategoryTilesState {
  items: TileState[];
}
export interface ProductRowState {
  title: string;
  source: RowSource;
  categoryId: number | null;
  productIds: number[];
  limit: number;
}
export interface TextState {
  heading: string;
  body: string;
}
export interface BannerState {
  mediaId: number | null;
  alt: string;
  link: string;
  entity: LinkEntity | null;
  height: BannerHeight;
}

interface BlockBase {
  id: string;
  enabled: boolean;
}
export type BlockState =
  | (BlockBase & { type: 'hero'; data: HeroState })
  | (BlockBase & { type: 'categoryTiles'; data: CategoryTilesState })
  | (BlockBase & { type: 'productRow'; data: ProductRowState })
  | (BlockBase & { type: 'text'; data: TextState })
  | (BlockBase & { type: 'banner'; data: BannerState });

export const newSlide = (): SlideState => ({ mediaId: null, alt: '', title: '', text: '', linkLabel: '', link: '', entity: null });

export function newBlock(type: BlockType, id: string): BlockState {
  switch (type) {
    case 'hero':
      return { id, type, enabled: true, data: { autoplaySeconds: 0, slides: [newSlide()] } };
    case 'categoryTiles':
      return { id, type, enabled: true, data: { items: [] } };
    case 'productRow':
      return { id, type, enabled: true, data: { title: '', source: 'newest', categoryId: null, productIds: [], limit: 8 } };
    case 'text':
      return { id, type, enabled: true, data: { heading: '', body: '' } };
    case 'banner':
      return { id, type, enabled: true, data: { mediaId: null, alt: '', link: '', entity: null, height: 'md' } };
  }
}

/** Moves one element by `delta` positions (-1 up, +1 down); out-of-range moves return the list unchanged. */
export function moveItem<T>(list: readonly T[], index: number, delta: number): T[] {
  const target = index + delta;
  if (index < 0 || index >= list.length || target < 0 || target >= list.length) return [...list];
  const next = [...list];
  [next[index], next[target]] = [next[target], next[index]];
  return next;
}

export const removeAt = <T>(list: readonly T[], index: number): T[] => list.filter((_, i) => i !== index);

export const replaceAt = <T>(list: readonly T[], index: number, value: T): T[] => list.map((item, i) => (i === index ? value : item));

export const canAddBlock = (blocks: readonly unknown[]) => blocks.length < HOME_LIMITS.blocks;

/** Number of errors under `blocks[index]` (payload paths from the API), to flag a block card and not only its fields. */
export function blockErrorCount(errors: Readonly<Record<string, string | undefined>>, index: number): number {
  const prefix = `blocks[${index}]`;
  return Object.keys(errors).filter((key) => errors[key] && (key === prefix || key.startsWith(`${prefix}.`) || key.startsWith(`${prefix}[`))).length;
}

const orNull = (value: string): string | null => (value.trim() === '' ? null : value);

/** Stored payload → editor state. Unknown block types are dropped (the server does the same). */
export function fromPayload(payload: HomePayload | null | undefined): BlockState[] {
  const blocks: BlockState[] = [];
  for (const block of payload?.blocks ?? []) {
    switch (block.type) {
      case 'hero':
        blocks.push({
          id: block.id,
          type: 'hero',
          enabled: block.enabled,
          data: {
            autoplaySeconds: block.data.autoplaySeconds,
            slides: block.data.slides.map((s) => ({ mediaId: s.mediaId, alt: s.alt, title: s.title, text: s.text, linkLabel: s.linkLabel, link: s.link ?? '', entity: s.entity ?? null })),
          },
        });
        break;
      case 'categoryTiles':
        blocks.push({ id: block.id, type: 'categoryTiles', enabled: block.enabled, data: { items: block.data.items.map((i) => ({ categoryId: i.categoryId, label: i.label ?? '' })) } });
        break;
      case 'productRow':
        blocks.push({ id: block.id, type: 'productRow', enabled: block.enabled, data: { ...block.data, productIds: [...block.data.productIds] } });
        break;
      case 'text':
        blocks.push({ id: block.id, type: 'text', enabled: block.enabled, data: { ...block.data } });
        break;
      case 'banner':
        blocks.push({ id: block.id, type: 'banner', enabled: block.enabled, data: { mediaId: block.data.mediaId, alt: block.data.alt, link: block.data.link ?? '', entity: block.data.entity ?? null, height: block.data.height } });
        break;
    }
  }
  return blocks;
}

/** Editor state → payload for `PUT /admin/site/home/draft`. Empty optional links and labels become `null`. */
export function toPayload(blocks: readonly BlockState[]): HomePayload {
  return {
    blocks: blocks.map((block): HomeBlockPayload => {
      switch (block.type) {
        case 'hero':
          return {
            id: block.id,
            type: 'hero',
            enabled: block.enabled,
            data: { autoplaySeconds: block.data.autoplaySeconds, slides: block.data.slides.map((s) => ({ ...s, ...optionalLink({ link: s.link, entity: s.entity }) })) },
          };
        case 'categoryTiles':
          return { id: block.id, type: 'categoryTiles', enabled: block.enabled, data: { items: block.data.items.map((i) => ({ categoryId: i.categoryId, label: orNull(i.label) })) } };
        case 'productRow':
          return { id: block.id, type: 'productRow', enabled: block.enabled, data: { ...block.data } };
        case 'text':
          return { id: block.id, type: 'text', enabled: block.enabled, data: { ...block.data } };
        case 'banner':
          // No image yet: 0 is never a media id, so the server answers with the error under `…data.mediaId`.
          return { id: block.id, type: 'banner', enabled: block.enabled, data: { mediaId: block.data.mediaId ?? 0, alt: block.data.alt, ...optionalLink({ link: block.data.link, entity: block.data.entity }), height: block.data.height } };
      }
    }),
  };
}

/** What the Server Action reads: the one hidden `payload` field; anything that is not a block list is rejected. */
export function parsePayloadField(raw: unknown): HomePayload {
  try {
    const value = JSON.parse(String(raw ?? '')) as { blocks?: unknown };
    if (value && Array.isArray(value.blocks)) return value as HomePayload;
  } catch {
    // falls through to the empty payload: the API answers with its own validation message
  }
  return { blocks: [] };
}
