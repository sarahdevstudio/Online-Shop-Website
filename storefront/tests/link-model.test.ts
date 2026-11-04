import { describe, expect, it } from 'vitest';
import { customValue, entityValue, isHidden, kindOf, optionalLink, requiredLink, targetKey, targetStatus, withKind } from '../src/lib/admin/link-model';
import type { LinkTarget } from '../src/lib/admin/site-types';

const category = { type: 'category', id: 4 } as const;

describe('link model', () => {
  it('knows its kind: custom unless an entity is set', () => {
    expect(kindOf(customValue('/x'))).toBe('custom');
    expect(kindOf(entityValue(category))).toBe('category');
  });

  it('picking an entity drops the typed URL; the server also lets the entity win', () => {
    expect(entityValue({ type: 'product', id: 9 })).toEqual({ link: null, entity: { type: 'product', id: 9, label: null } });
  });

  it('keeps the target name with the reference, so a deleted target can still be named', () => {
    expect(entityValue({ type: 'product', id: 9, label: 'چراغ' }).entity).toEqual({ type: 'product', id: 9, label: 'چراغ' });
  });

  it('switching kind keeps the typed URL on the way back to custom and the entity only for the same kind', () => {
    const picked = entityValue(category);
    expect(withKind(picked, 'custom', '/typed')).toEqual({ link: '/typed', entity: null });
    expect(withKind(picked, 'category', '/typed')).toEqual(picked);
    expect(withKind(picked, 'product', '/typed')).toEqual({ link: null, entity: null }); // another kind: nothing picked yet
  });

  it('an optional link is null when empty and never carries both', () => {
    expect(optionalLink({ link: '  ', entity: null })).toEqual({ link: null, entity: null });
    expect(optionalLink({ link: '/a', entity: null })).toEqual({ link: '/a', entity: null });
    expect(optionalLink({ link: '/a', entity: category })).toEqual({ link: null, entity: category });
  });

  it('a required link is an empty string (not null) while it points to an entity', () => {
    expect(requiredLink({ link: null, entity: category })).toEqual({ link: '', entity: category });
    expect(requiredLink({ link: '/a', entity: null })).toEqual({ link: '/a', entity: null });
  });

  it('reports a deleted target (named from the stored label), an unknown one, a live one and a hidden one', () => {
    const known: Record<string, LinkTarget> = {
      [targetKey(category)]: { ...category, label: 'لوستر', path: '/shop/lustre', exists: true, state: 'published' },
      'category:5': { type: 'category', id: 5, label: '', path: null, exists: false, state: 'deleted' },
      'product:7': { type: 'product', id: 7, label: 'چراغ', path: '/p/lamp', exists: true, state: 'draft' },
    };
    expect(targetStatus(category, known)).toEqual({ label: 'لوستر', path: '/shop/lustre', deleted: false, state: 'published' });
    expect(targetStatus({ type: 'category', id: 5, label: 'پرده' }, known)).toEqual({ label: 'پرده', path: null, deleted: true, state: 'deleted' });
    expect(targetStatus({ type: 'category', id: 5 }, known)?.label).toBe('');
    expect(targetStatus({ type: 'category', id: 6, label: 'x' }, known)).toEqual({ label: 'x', path: null, deleted: false, state: 'published' });
    expect(targetStatus(null, known)).toBeNull();
    expect(isHidden(targetStatus({ type: 'product', id: 7 }, known))).toBe(true);
    expect(isHidden(targetStatus(category, known))).toBe(false);
    expect(isHidden(targetStatus({ type: 'category', id: 5 }, known))).toBe(true);
  });
});
