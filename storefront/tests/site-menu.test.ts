import { describe, expect, it } from 'vitest';
import { buildMenu } from '../src/lib/site-menu';

const categories = [
  { slug: 'lamps', name: 'لامپ', children: [{ slug: 'led', name: 'ال‌ای‌دی' }] },
  { slug: 'chandeliers', name: 'لوستر', children: [] },
];

describe('buildMenu', () => {
  it('is today\'s menu (categories + blog) when nothing is configured', () => {
    expect(buildMenu([], categories)).toEqual([
      { label: 'لامپ', href: '/category/lamps' },
      { label: 'لوستر', href: '/category/chandeliers' },
      { label: 'وبلاگ', href: '/blog' },
    ]);
  });

  it('expands an "all categories" entry into the category tree and ignores its own children', () => {
    const menu = buildMenu([{ label: 'فروشگاه', link: '/', allCategories: true, children: [{ label: 'x', link: '/x' }] }], categories);
    expect(menu).toHaveLength(1);
    expect(menu[0].children?.map((c) => c.href)).toEqual(['/category/lamps', '/category/chandeliers']);
    expect(menu[0].children?.[0].children).toEqual([{ label: 'ال‌ای‌دی', href: '/category/led' }]);
  });

  it('keeps configured links and one level of children', () => {
    const menu = buildMenu([{ label: 'درباره ما', link: '/about', allCategories: false, children: [{ label: 'تماس', link: '/contact' }] }], categories);
    expect(menu).toEqual([{ label: 'درباره ما', href: '/about', children: [{ label: 'تماس', href: '/contact' }] }]);
  });
});
