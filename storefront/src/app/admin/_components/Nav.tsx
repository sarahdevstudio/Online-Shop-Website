'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { adminMessages } from '@/lib/admin/messages';
import styles from '../admin.module.css';

const ITEMS = [
  { href: '/admin', label: adminMessages.nav.dashboard, exact: true },
  { href: '/admin/orders', label: adminMessages.nav.orders },
  { href: '/admin/products', label: adminMessages.nav.products },
  { href: '/admin/categories', label: adminMessages.nav.categories },
  { href: '/admin/brands', label: adminMessages.nav.brands },
  { href: '/admin/content', label: adminMessages.nav.content },
  { href: '/admin/blog', label: adminMessages.nav.blog },
  { href: '/admin/settings', label: adminMessages.nav.settings },
  { href: '/admin/account', label: adminMessages.nav.account },
];

export function Nav() {
  const pathname = usePathname();
  return (
    <nav aria-label={adminMessages.title} className={styles.nav}>
      <ul className={styles.nav}>
        {ITEMS.map((item) => {
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
