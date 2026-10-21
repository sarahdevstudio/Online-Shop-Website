import Link from 'next/link';
import type { ReactNode } from 'react';
import { Icon } from './Icon';
import type { NavItem } from './SiteHeader';
import { messages } from '@/lib/messages';
import styles from './SiteFooter.module.css';

export interface FooterGroup {
  title: string;
  links: readonly NavItem[];
}

interface SiteFooterProps {
  brandName: string;
  about?: string;
  groups?: readonly FooterGroup[];
  phone?: string;
  email?: string;
  address?: string;
  copyright: string;
  /** Trust marks (e.g. e-Namad) supplied by the shop owner. */
  badges?: ReactNode;
}

export function SiteFooter({ brandName, about, groups = [], phone, email, address, copyright, badges }: SiteFooterProps) {
  return (
    <footer className={styles.footer}>
      <div className={`ui-container ${styles.grid}`}>
        <section>
          <p className={styles.brand}>{brandName}</p>
          {about && <p className={styles.text}>{about}</p>}
          <address className={styles.contact}>
            {phone && (
              <a href={`tel:${phone}`}>
                <Icon name="phone" /> <bdi dir="ltr">{phone}</bdi>
              </a>
            )}
            {email && (
              <a href={`mailto:${email}`}>
                <Icon name="mail" /> <bdi dir="ltr">{email}</bdi>
              </a>
            )}
            {address && (
              <span>
                <Icon name="map-pin" /> {address}
              </span>
            )}
          </address>
        </section>

        <nav className={styles.groups} aria-label={messages.footerNavigation}>
          {groups.map((group) => (
            <section key={group.title}>
              <h2 className={styles.groupTitle}>{group.title}</h2>
              <ul>
                {group.links.map((link) => (
                  <li key={link.href}>
                    <Link href={link.href}>{link.label}</Link>
                  </li>
                ))}
              </ul>
            </section>
          ))}
        </nav>

        {badges && <div className={styles.badges}>{badges}</div>}
      </div>
      <div className={styles.legal}>
        <div className="ui-container">
          <small>{copyright}</small>
        </div>
      </div>
    </footer>
  );
}
