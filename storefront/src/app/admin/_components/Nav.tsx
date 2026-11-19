'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { adminMessages } from '@/lib/admin/messages';
import styles from '../admin.module.css';

/** `permission`: the item shows only for accounts that hold it (no `permission`: everyone). The API checks every call anyway. */
const items = (): { href: string; label: string; exact?: boolean; permission?: string }[] => [
  { href: '/admin', label: adminMessages.nav.dashboard, exact: true },
  { href: '/admin/orders', permission: 'Orders.View', label: adminMessages.nav.orders },
  { href: '/admin/products', permission: 'Catalog.View', label: adminMessages.nav.products },
  { href: '/admin/categories', permission: 'Catalog.View', label: adminMessages.nav.categories },
  { href: '/admin/brands', permission: 'Catalog.View', label: adminMessages.nav.brands },
  { href: '/admin/content', permission: 'Content.View', label: adminMessages.nav.content },
  { href: '/admin/blog', permission: 'Blog.View', label: adminMessages.nav.blog },
  { href: '/admin/site', permission: 'Site.View', label: adminMessages.nav.site },
  { href: '/admin/redirects', permission: 'Routes.View', label: adminMessages.nav.routes },
  { href: '/admin/translations', permission: 'Translations.View', label: adminMessages.nav.translations },
  { href: '/admin/settings', permission: 'Settings.View', label: adminMessages.nav.settings },
  { href: '/admin/reviews', permission: 'Reviews.View', label: adminMessages.nav.reviews },
  { href: '/admin/reports', permission: 'Reports.View', label: adminMessages.nav.reports },
  { href: '/admin/discounts', permission: 'Discounts.View', label: adminMessages.nav.discounts },
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
