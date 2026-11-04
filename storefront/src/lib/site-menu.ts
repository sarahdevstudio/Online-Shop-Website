import type { MenuNode } from '@/components/SiteHeader';
import { messages } from './messages';
import type { MenuItem } from './site-config';

interface CategoryNode {
  slug: string;
  name: string;
  path: string;
  children: readonly { slug: string; name: string; path: string }[];
}

/** The configured header menu; an "all categories" entry is expanded into the real category tree. An empty configuration = today's menu. */
export function buildMenu(items: readonly MenuItem[], categories: readonly CategoryNode[]): MenuNode[] {
  const categoryNodes: MenuNode[] = categories.map((c) => ({
    label: c.name,
    href: c.path,
    children: c.children.map((child) => ({ label: child.name, href: child.path })),
  }));
  if (items.length === 0) return [...categoryNodes.map(({ label, href }) => ({ label, href })), { label: messages.blog, href: '/blog' }];
  return items.map((item) => ({
    label: item.label,
    href: item.link,
    children: item.allCategories ? categoryNodes : item.children.map((child) => ({ label: child.label, href: child.link })),
  }));
}
