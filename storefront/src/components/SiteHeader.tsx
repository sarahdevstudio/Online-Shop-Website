'use client';

import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';
import { useState, type FormEvent } from 'react';
import { Icon } from './Icon';
import { formatNumber } from '@/lib/format';
import { messages } from '@/lib/messages';
import styles from './SiteHeader.module.css';

export interface NavItem {
  label: string;
  href: string;
}

interface SiteHeaderProps {
  brandName: string;
  navItems?: readonly NavItem[];
  cartCount?: number;
  /** Signed-in customer's name; omit when signed out. */
  accountLabel?: string | null;
}

/** The page must contain <main id="main" tabIndex={-1}> as the skip-link target. */
export function SiteHeader({ brandName, navItems = [], cartCount = 0, accountLabel }: SiteHeaderProps) {
  const pathname = usePathname();
  const router = useRouter();
  const [menuOpen, setMenuOpen] = useState(false);

  const cartText = cartCount > 99 ? `${formatNumber(99)}+` : formatNumber(cartCount);
  const cartLabel = cartCount > 0 ? messages.cartWithCount(formatNumber(cartCount)) : messages.cart;
  const isActive = (href: string) => pathname === href || pathname.startsWith(`${href}/`);

  function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const value = String(new FormData(event.currentTarget).get('q') ?? '').trim();
    if (value) router.push(`/search?q=${encodeURIComponent(value)}`);
  }

  const links = (onClick?: () => void) =>
    navItems.map((item) => (
      <li key={item.href}>
        <Link
          href={item.href}
          className={isActive(item.href) ? styles.active : undefined}
          aria-current={isActive(item.href) ? 'page' : undefined}
          onClick={onClick}
        >
          {item.label}
        </Link>
      </li>
    ));

  return (
    <header className={styles.header}>
      <a className="ui-skip-link" href="#main">
        {messages.skipToContent}
      </a>
      <div className={`ui-container ${styles.bar}`}>
        <button
          type="button"
          className={`${styles.iconBtn} ${styles.menuBtn}`}
          aria-label={messages.menu}
          aria-expanded={menuOpen}
          aria-controls="mobile-menu"
          onClick={() => setMenuOpen((open) => !open)}
        >
          <Icon name={menuOpen ? 'x' : 'menu'} />
        </button>

        <Link className={styles.brand} href="/">
          <span>{brandName}</span>
        </Link>

        <form className={styles.search} role="search" onSubmit={onSubmit}>
          <label className="ui-visually-hidden" htmlFor="header-search">
            {messages.searchLabel}
          </label>
          <input
            id="header-search"
            className="ui-control"
            type="search"
            name="q"
            autoComplete="off"
            enterKeyHint="search"
            placeholder={messages.searchPlaceholder}
          />
          <button type="submit" className={styles.searchBtn} aria-label={messages.searchSubmit}>
            <Icon name="search" />
          </button>
        </form>

        <div className={styles.actions}>
          <Link className={styles.iconBtn} href="/account" aria-label={accountLabel ?? messages.account}>
            <Icon name="user" />
            {accountLabel && <span className={styles.accountName}>{accountLabel}</span>}
          </Link>
          <Link className={styles.iconBtn} href="/cart" aria-label={cartLabel}>
            <Icon name="cart" />
            {cartCount > 0 && (
              <span className={styles.count} aria-hidden="true">
                {cartText}
              </span>
            )}
          </Link>
        </div>
      </div>

      <nav className={styles.primary} aria-label={messages.mainNavigation}>
        <ul className="ui-container">{links()}</ul>
      </nav>

      {menuOpen && (
        <nav id="mobile-menu" className={`${styles.menuPanel} ui-container`} aria-label={messages.mainNavigation}>
          <ul className={styles.drawerNav}>{links(() => setMenuOpen(false))}</ul>
        </nav>
      )}
    </header>
  );
}
