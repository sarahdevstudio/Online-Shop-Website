'use client';

import Link from 'next/link';
import { useState } from 'react';
import { Icon } from './Icon';
import { dismissalCookie } from '@/lib/announcement';
import { messages } from '@/lib/messages';
import styles from './AnnouncementBar.module.css';

interface AnnouncementBarProps {
  text: string;
  href: string | null;
  style: 'primary' | 'accent' | 'neutral';
  dismissible: boolean;
}

/**
 * A short plain-text notice above the header. Closing it stores a cookie (lib/announcement) that the server reads, so a visitor who
 * closed it never gets the bar rendered and then pulled out of the page after hydration.
 */
export function AnnouncementBar({ text, href, style, dismissible }: AnnouncementBarProps) {
  const [dismissed, setDismissed] = useState(false);

  if (dismissed) return null;

  const isExternal = href !== null && /^https:/i.test(href);
  const content = href ? (
    isExternal ? (
      <a href={href} rel="noopener noreferrer nofollow" target="_blank">
        {text}
      </a>
    ) : (
      <Link href={href}>{text}</Link>
    )
  ) : (
    <span>{text}</span>
  );

  return (
    <div className={`${styles.bar} ${styles[style]}`} role="region" aria-label={messages.announcement}>
      <div className={`ui-container ${styles.inner}`}>
        <p className={styles.text}>{content}</p>
        {dismissible && (
          <button
            type="button"
            className={styles.close}
            aria-label={messages.closeAnnouncement}
            onClick={() => {
              setDismissed(true);
              document.cookie = dismissalCookie(text);
            }}
          >
            <Icon name="x" />
          </button>
        )}
      </div>
    </div>
  );
}
