import { describe, expect, it, vi } from 'vitest';
import type { AdminResult } from '../src/lib/admin/api';
import { applyPathChange, type PathChangeDeps } from '../src/lib/admin/route-actions';
import { normalizePathInput, pathProblemState, resultingUrl } from '../src/lib/admin/route-form';
import { problemState } from '../src/lib/admin/form-state';
import { adminMessages } from '../src/lib/admin/messages';
import type { RouteTarget } from '../src/lib/admin/types';

const target: RouteTarget = { type: 'product', id: 7, path: '/shop/x', rowVersion: 'AAA=', oldPaths: [] };
const ok = (): AdminResult<RouteTarget> => ({ ok: true, status: 200, body: target, setCookies: [] });
const fail = (status: number, code: string | null, detail: string | null, errors: Record<string, string[]> | null = null): AdminResult<RouteTarget> =>
  ({ ok: false, status, problem: { status, code, detail, errors } });

function form(fields: Record<string, string>) {
  const data = new FormData();
  for (const [key, value] of Object.entries(fields)) data.set(key, value);
  return data;
}

function deps(result: AdminResult<RouteTarget>) {
  const put = vi.fn().mockResolvedValue(result);
  const refresh = vi.fn();
  return { put, refresh, deps: { put, refresh } satisfies PathChangeDeps };
}

describe('normalizing what the admin typed (mirrors the API)', () => {
  it('trims, lower-cases, adds the leading slash and drops the trailing one', () => {
    expect(normalizePathInput('  /Shop/LED-9W/ ')).toBe('/shop/led-9w');
    expect(normalizePathInput('shop/x')).toBe('/shop/x');
    expect(normalizePathInput('   ')).toBe('');
    expect(normalizePathInput('/')).toBe('');
  });

  it('shows the full URL the visitor will see, or the default path when nothing is typed', () => {
    expect(resultingUrl('https://shop.example/', 'Lustre/Crystal/', '/product/x')).toBe('https://shop.example/lustre/crystal');
    expect(resultingUrl('https://shop.example', '', '/product/x')).toBe('https://shop.example/product/x');
  });
});

describe('applying a path change from an edit form', () => {
  it('sends nothing when the path field was not edited (even if typed with a different spelling)', async () => {
    const d = deps(ok());

    const result = await applyPathChange('product', 7, form({ path: '/Shop/X/', currentPath: '/shop/x', path_rowVersion: 'AAA=' }), {}, d.deps);

    expect(result).toEqual({ ok: true, changed: false });
    expect(d.put).not.toHaveBeenCalled();
    expect(d.refresh).not.toHaveBeenCalled();
  });

  it('sends the typed path with the loaded version, then drops the public caches so the new path shows at once', async () => {
    const d = deps(ok());

    const result = await applyPathChange('product', 7, form({ path: '/lustre/x', currentPath: '/shop/x', path_rowVersion: 'AAA=' }), {}, d.deps);

    expect(result).toEqual({ ok: true, changed: true });
    expect(d.put).toHaveBeenCalledWith('/admin/routes/product/7', { path: '/lustre/x', rowVersion: 'AAA=' });
    expect(d.refresh).toHaveBeenCalledTimes(1); // cache invalidation on change
  });

  it('shows a taken path next to the field, with the API\'s Persian text, and invalidates nothing', async () => {
    const message = 'این نشانی هم‌اکنون برای «محصول دیگر» استفاده می‌شود.';
    const d = deps(fail(409, 'path_taken', message));

    const result = await applyPathChange('product', 7, form({ path: '/other', currentPath: '/shop/x', path_rowVersion: 'AAA=' }), { path: '/other' }, d.deps);

    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.state.errors).toEqual({ path: message });
      expect(result.state.conflict).toBe(false);
      expect(result.state.submitted).toEqual({ path: '/other' });
    }
    expect(d.refresh).not.toHaveBeenCalled();
  });

  it('shows validation and reserved-path refusals (400) on the field too', async () => {
    const d = deps(fail(400, 'validation_failed', 'x', { path: ['این نشانی نگه داشته شده است.'] }));

    const result = await applyPathChange('category', 3, form({ path: '/cart', currentPath: '/category/a', path_rowVersion: 'AAA=' }), {}, d.deps);

    expect(!result.ok && result.state.errors.path).toBe('این نشانی نگه داشته شده است.');
  });

  it('a stale version is the usual "data changed" conflict', async () => {
    const d = deps(fail(409, 'concurrent_update', 'x'));

    const result = await applyPathChange('blogPost', 3, form({ path: '/a', currentPath: '/b', path_rowVersion: 'OLD=' }), {}, d.deps);

    expect(!result.ok && result.state.conflict).toBe(true);
  });

  it('a missing permission is the forbidden message, not a crash', async () => {
    const d = deps(fail(403, 'forbidden', 'x'));

    const result = await applyPathChange('contentPage', 3, form({ path: '/a', currentPath: '/b', path_rowVersion: 'A=' }), {}, d.deps);

    expect(!result.ok && result.state.message).toBe(adminMessages.forbidden);
  });
});

describe('creating with a path the API refused', () => {
  it('maps a 409 path refusal to the path field', () => {
    const state = problemState({ status: 409, code: 'path_taken_redirect', detail: 'قبلاً استفاده می‌شد', errors: null }, { path: '/old' });
    expect(state.errors).toEqual({ path: 'قبلاً استفاده می‌شد' });
    expect(state.conflict).toBe(false);
  });

  it('keeps unrelated problems out of the path mapper', () => {
    expect(pathProblemState({ status: 500, code: null, detail: 'x', errors: null }, {})).toBeNull();
    expect(pathProblemState({ status: 400, code: 'validation_failed', detail: 'x', errors: { Name: ['x'] } }, {})).toBeNull();
  });
});
