import { describe, expect, it } from 'vitest';
import { isPageInRange, pageOneRedirect, parsePageParam } from '../src/lib/paging';

describe('pageOneRedirect', () => {
  it('redirects ?page=1 to the clean URL', () => {
    expect(pageOneRedirect('/category/clothing', { page: '1' })).toBe('/category/clothing');
  });

  it('keeps other params, in order, and encodes them', () => {
    expect(pageOneRedirect('/search', { q: 'لامپ E27', page: '1', sort: 'new' })).toBe(
      '/search?q=%D9%84%D8%A7%D9%85%D9%BE+E27&sort=new',
    );
  });

  it('keeps repeated params', () => {
    expect(pageOneRedirect('/search', { page: '1', brand: ['a', 'b'] })).toBe('/search?brand=a&brand=b');
  });

  it.each([undefined, '2', '01', 'abc', ['1', '2']])('does not redirect page=%j', (page) => {
    expect(pageOneRedirect('/search', { page })).toBeNull();
  });
});

describe('parsePageParam', () => {
  it('treats a missing value as page 1', () => {
    expect(parsePageParam(undefined)).toBe(1);
  });

  it('accepts plain positive integers', () => {
    expect(parsePageParam('1')).toBe(1);
    expect(parsePageParam('2')).toBe(2);
    expect(parsePageParam('999999')).toBe(999999);
  });

  it.each(['', '0', '-1', '2.5', '01', 'abc', '1e3', ' 2', '2 ', '+2', '1000000', '۲'])('rejects %j', (value) => {
    expect(parsePageParam(value)).toBeNull();
  });
});

describe('isPageInRange', () => {
  it('accepts pages 1..pageCount', () => {
    expect(isPageInRange(1, 3)).toBe(true);
    expect(isPageInRange(3, 3)).toBe(true);
  });

  it('rejects pages beyond the last or below 1', () => {
    expect(isPageInRange(4, 3)).toBe(false);
    expect(isPageInRange(0, 3)).toBe(false);
  });

  it('keeps page 1 valid for an empty listing', () => {
    expect(isPageInRange(1, 0)).toBe(true);
    expect(isPageInRange(2, 0)).toBe(false);
  });
});
