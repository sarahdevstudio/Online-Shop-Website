import { describe, expect, it } from 'vitest';
import { BLOCK_TYPES, HOME_LIMITS, blockErrorCount, canAddBlock, fromPayload, moveItem, newBlock, parsePayloadField, removeAt, replaceAt, toPayload } from '../src/lib/admin/site-home-model';
import type { HomePayload } from '../src/lib/admin/site-types';

const stored: HomePayload = {
  blocks: [
    { id: 'a', type: 'hero', enabled: true, data: { autoplaySeconds: 0, slides: [{ mediaId: null, alt: '', title: 'T', text: 'X', linkLabel: 'L', link: '/search' }] } },
    { id: 'b', type: 'categoryTiles', enabled: false, data: { items: [{ categoryId: 1, label: null }, { categoryId: 2, label: 'لوستر' }] } },
    { id: 'c', type: 'productRow', enabled: true, data: { title: 'جدید', source: 'handpicked', categoryId: null, productIds: [3, 4], limit: 8 } },
    { id: 'd', type: 'text', enabled: true, data: { heading: 'H', body: 'B' } },
    { id: 'e', type: 'banner', enabled: true, data: { mediaId: 7, alt: 'alt', link: null, height: 'lg' } },
  ],
};

describe('home block model', () => {
  it('creates every block type with valid starting data', () => {
    for (const type of BLOCK_TYPES) {
      const block = newBlock(type, `id-${type}`);
      expect(block.type).toBe(type);
      expect(block.enabled).toBe(true);
    }
    const hero = newBlock('hero', 'h');
    expect(hero.type === 'hero' && hero.data.slides).toHaveLength(1);
    const row = newBlock('productRow', 'r');
    expect(row.type === 'productRow' && row.data.limit).toBe(8);
  });

  it('round-trips a stored payload (null links and labels become empty strings and back)', () => {
    const state = fromPayload(stored);
    expect(state).toHaveLength(5);
    expect(state[1].type === 'categoryTiles' && state[1].data.items[0].label).toBe('');
    expect(state[4].type === 'banner' && state[4].data.link).toBe('');
    expect(toPayload(state)).toEqual(stored);
  });

  it('drops unknown block types and tolerates a missing payload', () => {
    expect(fromPayload({ blocks: [{ id: 'x', type: 'carousel', enabled: true, data: {} } as never] })).toEqual([]);
    expect(fromPayload(null)).toEqual([]);
  });

  it('sends media id 0 for a banner without an image so the server names the field', () => {
    const payload = toPayload([newBlock('banner', 'b')]);
    expect(payload.blocks[0].type === 'banner' && payload.blocks[0].data.mediaId).toBe(0);
  });

  it('moves, removes and replaces without mutating', () => {
    const list = ['a', 'b', 'c'];
    expect(moveItem(list, 1, -1)).toEqual(['b', 'a', 'c']);
    expect(moveItem(list, 1, 1)).toEqual(['a', 'c', 'b']);
    expect(moveItem(list, 0, -1)).toEqual(['a', 'b', 'c']);
    expect(moveItem(list, 2, 1)).toEqual(['a', 'b', 'c']);
    expect(removeAt(list, 1)).toEqual(['a', 'c']);
    expect(replaceAt(list, 2, 'z')).toEqual(['a', 'b', 'z']);
    expect(list).toEqual(['a', 'b', 'c']);
  });

  it('caps the number of blocks', () => {
    const blocks = Array.from({ length: HOME_LIMITS.blocks }, (_, i) => newBlock('text', String(i)));
    expect(canAddBlock(blocks)).toBe(false);
    expect(canAddBlock(blocks.slice(1))).toBe(true);
  });

  it('counts the errors of one block by payload path', () => {
    const errors = { 'blocks[0].data.title': 'x', 'blocks[0].enabled': 'y', 'blocks[10].data.title': 'z', 'blocks[1]': 'w' };
    expect(blockErrorCount(errors, 0)).toBe(2);
    expect(blockErrorCount(errors, 1)).toBe(1);
    expect(blockErrorCount(errors, 10)).toBe(1);
    expect(blockErrorCount(errors, 2)).toBe(0);
  });

  it('parses the posted payload field defensively', () => {
    expect(parsePayloadField('{"blocks":[]}')).toEqual({ blocks: [] });
    expect(parsePayloadField('{"blocks":5}')).toEqual({ blocks: [] });
    expect(parsePayloadField('not json')).toEqual({ blocks: [] });
    expect(parsePayloadField(null)).toEqual({ blocks: [] });
  });
});
