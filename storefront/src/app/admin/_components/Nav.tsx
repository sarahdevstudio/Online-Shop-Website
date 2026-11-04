'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { adminMessages } from '@/lib/admin/messages';
import styles from '../admin.module.css';

/** `permission`: the item shows only for accounts that hold it (no `permission`: everyone). The API checks every call anyway. */
const items = (): { href: string; label: string; exact?: boolean; permission?: string }[] => [
  { href: '/admin', label: adminMessages.nav.dashboard, exact: true },
  { href: '/admin/orders', permission: 'Orders.Manage', label: adminMessages.nav.orders },
  { href: '/admin/products', permission: 'Catalog.Manage', label: adminMessages.nav.products },
  { href: '/admin/categories', permission: 'Catalog.Manage', label: adminMessages.nav.categories },
  { href: '/admin/brands', permission: 'Catalog.Manage', label: adminMessages.nav.brands },
  { href: '/admin/content', permission: 'Content.Manage', label: adminMessages.nav.content },
  { href: '/admin/blog', permission: 'Blog.Manage', label: adminMessages.nav.blog },
  { href: '/admin/site', permission: 'Site.Manage', label: adminMessages.nav.site },
  { href: '/admin/redirects', permission: 'Routes.Manage', label: adminMessages.nav.routes },
  { href: '/admin/translations', permission: 'Translations.Manage', label: adminMessages.nav.translations },
  { href: '/admin/settings', permission: 'Settings.Manage', label: adminMessages.nav.settings },
  { href: '/admin/staff', permission: 'User.Manage', label: adminMessages.nav.staff },
  { href: '/admin/account', label: adminMessages.nav.account },
];

export function Nav({ permissions }: { permissions?: string[] }) {
  const pathname = usePathname();
  return (
    <nav aria-label={adminMessages.title} className={styles.navWrap}>
      <ul className={styles.nav}>
        {items().filter((item) => !item.permission || !permissions || permissions.includes(item.permission)).map((item) => {
          const current = item.exact ? pathname === item.href : pathname === item.href || pathname.startsWith(`${item.href}/`);
          return (
            <li key={item.href}>
              <Link href={item.href} aria-current={current ? 'page' : undefined}>
                {item.label}
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
