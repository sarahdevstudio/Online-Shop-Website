import { describe, expect, it } from 'vitest';
import { isInternalPath, isResolvablePath, pathFromSegments, redirectUrl } from '../src/lib/routes';

describe('which request paths are worth resolving', () => {
  it('accepts plain 1-4 segment ASCII paths of either case', () => {
    for (const path of ['/about', '/category/chandeliers', '/lustre/crystal', '/a/b/c/d', '/Product/LED-9w', '/product/9']) {
      expect(isResolvablePath(path), path).toBe(true);
    }
  });

  it('rejects everything else without a call', () => {
    for (const path of ['/', '', '/a//b', '/a/b/c/d/e', '/wp-login.php', '/.env', '/a_b', '/-a', '/a-', '/a--b', '/لوستر', '/a b', `/${'a'.repeat(200)}`]) {
      expect(isResolvablePath(path), path).toBe(false);
    }
  });

  it('builds the path from the catch-all segments', () => {
    expect(pathFromSegments(['product', 'x'])).toBe('/product/x');
    expect(pathFromSegments(undefined)).toBe('/');
  });
});

describe('redirects never leave the site', () => {
  it('allows only local absolute paths', () => {
    expect(isInternalPath('/product/x')).toBe(true);
    for (const to of ['//evil.example', 'https://evil.example/x', '/\\evil.example', 'product/x', '', '/a\nb']) {
      expect(isInternalPath(to), JSON.stringify(to)).toBe(false);
    }
  });
});

describe('redirect URL keeps the query string', () => {
  it('returns the bare path without a query', () => {
    expect(redirectUrl('/shop/lamps', {})).toBe('/shop/lamps');
    expect(redirectUrl('/shop/lamps', { page: undefined })).toBe('/shop/lamps');
  });

  it('keeps filters, sort and page, in the visitor\'s order', () => {
    expect(redirectUrl('/shop/lamps', { wattage: '9w', sort: 'price-asc', page: '2' })).toBe('/shop/lamps?wattage=9w&sort=price-asc&page=2');
  });

  it('keeps repeated keys and writes commas literally, like the listing URLs', () => {
    expect(redirectUrl('/shop/lamps', { brand: ['a', 'b'], wattage: '9w,12w' })).toBe('/shop/lamps?brand=a&brand=b&wattage=9w,12w');
  });

  it('encodes what must be encoded', () => {
    expect(redirectUrl('/x', { q: 'لامپ & led' })).toBe('/x?q=%D9%84%D8%A7%D9%85%D9%BE+%26+led');
  });
});
