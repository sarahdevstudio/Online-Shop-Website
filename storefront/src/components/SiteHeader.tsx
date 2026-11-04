'use client';

import Image from 'next/image';
import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';
import { useEffect, useRef, useState, type FormEvent } from 'react';
import { Form, TextField } from '@/components/form';
import { Icon } from './Icon';
import { formatNumber } from '@/lib/format';
import { messages } from '@/lib/messages';
import styles from './SiteHeader.module.css';

export interface NavItem {
  label: string;
  href: string;
}

/** A menu entry: a link, a group of links (dropdown), or both. */
export interface MenuNode {
  label: string;
  href: string | null;
  children?: readonly MenuNode[];
}

export interface HeaderLogo {
  url: string;
  alt: string;
  width: number;
  height: number;
}

interface SiteHeaderProps {
  brandName: string;
  logo?: HeaderLogo | null;
  menu?: readonly MenuNode[];
  cartCount?: number;
}

/** The page must contain <main id="main" tabIndex={-1}> as the skip-link target. */
export function SiteHeader({ brandName, logo = null, menu = [], cartCount = 0 }: SiteHeaderProps) {
  const pathname = usePathname();
  const router = useRouter();
  const [menuOpen, setMenuOpen] = useState(false);
  const [openSub, setOpenSub] = useState<number | null>(null);
  const navRef = useRef<HTMLElement>(null);

  const cartText = cartCount > 99 ? `${formatNumber(99)}+` : formatNumber(cartCount);
  const cartLabel = cartCount > 0 ? messages.cartWithCount(formatNumber(cartCount)) : messages.cart;
  const isActive = (href: string) => (href === '/' ? pathname === '/' : pathname === href || pathname.startsWith(`${href}/`));

  // A dropdown closes on Escape, on a click outside and when the page changes.
  useEffect(() => setOpenSub(null), [pathname]);
  useEffect(() => {
    if (openSub === null) return;
    const onPointer = (event: PointerEvent) => {
      if (!navRef.current?.contains(event.target as Node)) setOpenSub(null);
    };
    document.addEventListener('pointerdown', onPointer);
    return () => document.removeEventListener('pointerdown', onPointer);
  }, [openSub]);

  function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const value = String(new FormData(event.currentTarget).get('q') ?? '').trim();
    if (value) router.push(`/search?q=${encodeURIComponent(value)}`);
  }

  const linkClass = (href: string | null) => (href && isActive(href) ? styles.active : undefined);

  const desktopItem = (node: MenuNode, index: number) => {
    const children = node.children ?? [];
    if (children.length === 0) {
      return node.href ? (
        <li key={`${node.label}-${index}`}>
          <Link href={node.href} className={linkClass(node.href)} aria-current={isActive(node.href) ? 'page' : undefined}>
            {node.label}
          </Link>
        </li>
      ) : null;
    }
    const open = openSub === index;
    const panelId = `submenu-${index}`;
    return (
      <li
        key={`${node.label}-${index}`}
        className={styles.hasSub}
        onKeyDown={(event) => {
          if (event.key === 'Escape' && open) {
            setOpenSub(null);
            (event.currentTarget.querySelector('button') as HTMLElement | null)?.focus();
          }
        }}
        onBlur={(event) => {
          if (open && !event.currentTarget.contains(event.relatedTarget as Node | null)) setOpenSub(null);
        }}
      >
        {node.href && (
          <Link href={node.href} className={linkClass(node.href)} aria-current={isActive(node.href) ? 'page' : undefined}>
            {node.label}
          </Link>
        )}
        <button
          type="button"
          className={styles.subToggle}
          aria-expanded={open}
          aria-controls={panelId}
          aria-label={messages.submenuOf(node.label)}
          onClick={() => setOpenSub(open ? null : index)}
        >
          {!node.href && <span>{node.label}</span>}
          <span className={styles.chevron} aria-hidden="true" />
        </button>
        {open && (
          <ul id={panelId} className={styles.dropdown}>
            {children.map((child) => (
              <li key={`${child.label}-${child.href}`}>
                {child.href ? (
                  <Link href={child.href} aria-current={isActive(child.href) ? 'page' : undefined}>
                    {child.label}
                  </Link>
                ) : (
                  <span>{child.label}</span>
                )}
                {(child.children?.length ?? 0) > 0 && (
                  <ul className={styles.dropdownSub}>
                    {child.children!.map((grand) => (
                      <li key={`${grand.label}-${grand.href}`}>{grand.href ? <Link href={grand.href}>{grand.label}</Link> : grand.label}</li>
                    ))}
                  </ul>
                )}
              </li>
            ))}
          </ul>
        )}
      </li>
    );
  };

  const drawerItem = (node: MenuNode, index: number) => {
    const children = node.children ?? [];
    return (
      <li key={`${node.label}-${index}`}>
        {node.href ? (
          <Link href={node.href} className={linkClass(node.href)} aria-current={isActive(node.href) ? 'page' : undefined} onClick={() => setMenuOpen(false)}>
            {node.label}
          </Link>
        ) : (
          <span className={styles.drawerGroup}>{node.label}</span>
        )}
        {children.length > 0 && (
          <ul className={styles.drawerSub}>
            {children.map((child) => (
              <li key={`${child.label}-${child.href}`}>
                {child.href ? (
                  <Link href={child.href} className={linkClass(child.href)} onClick={() => setMenuOpen(false)}>
                    {child.label}
                  </Link>
                ) : (
                  <span className={styles.drawerGroup}>{child.label}</span>
                )}
              </li>
            ))}
          </ul>
        )}
      </li>
    );
  };

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
          {logo ? (
            <span className={styles.logoPlate}>
              {/* The logo is served by the media route; a fixed height keeps every aspect ratio safe. It is on every page and
                  above the fold, so it is preloaded and resized to the 11rem it is shown at (a 300 px PNG becomes a few KB of WebP). */}
              <Image className={styles.logo} src={logo.url} alt={logo.alt} width={logo.width} height={logo.height} sizes="176px" quality={90} priority />
            </span>
          ) : (
            <span>{brandName}</span>
          )}
        </Link>

        <Form className={styles.search} role="search" onSubmit={onSubmit}>
          <TextField name="q" label={messages.searchLabel} hideLabel type="search" autoComplete="off" enterKeyHint="search" placeholder={messages.searchPlaceholder} />
          <button type="submit" className={styles.searchBtn} aria-label={messages.searchSubmit}>
            <Icon name="search" />
          </button>
        </Form>

        <div className={styles.actions}>
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

      <nav ref={navRef} className={styles.primary} aria-label={messages.mainNavigation}>
        <ul className="ui-container">{menu.map(desktopItem)}</ul>
      </nav>

      {menuOpen && (
        <nav id="mobile-menu" className={`${styles.menuPanel} ui-container`} aria-label={messages.mainNavigation}>
          <ul className={styles.drawerNav}>{menu.map(drawerItem)}</ul>
        </nav>
      )}
    </header>
  );
}
