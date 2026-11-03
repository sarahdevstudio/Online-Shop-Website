'use client';

import Link from 'next/link';
import { useEffect, useState } from 'react';
import { Icon } from './Icon';
import { messages } from '@/lib/messages';
import styles from './AnnouncementBar.module.css';

interface AnnouncementBarProps {
  text: string;
  href: string | null;
  style: 'primary' | 'accent' | 'neutral';
  dismissible: boolean;
}

const KEY_PREFIX = 'announcement-dismissed:';

/** A short plain-text notice above the header. Dismissal is remembered per text in localStorage; if storage fails it simply shows. */
export function AnnouncementBar({ text, href, style, dismissible }: AnnouncementBarProps) {
  const [dismissed, setDismissed] = useState(false);

  useEffect(() => {
    try {
      if (window.localStorage.getItem(KEY_PREFIX + text) === '1') setDismissed(true);
    } catch {
      /* storage unavailable: show the bar */
    }
  }, [text]);

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
              try {
                window.localStorage.setItem(KEY_PREFIX + text, '1');
              } catch {
                /* ignore */
              }
            }}
          >
            <Icon name="x" />
          </button>
        )}
      </div>
    </div>
  );
}
