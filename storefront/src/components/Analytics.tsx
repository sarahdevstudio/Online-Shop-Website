'use client';

import { useEffect } from 'react';
import { GA4_ID } from '@/lib/site-config';

declare global {
  interface Window {
    dataLayer?: unknown[];
    gtag?: (...args: unknown[]) => void;
  }
}

/**
 * Google Analytics 4, only when the owner entered a measurement id (the layout does not render this component otherwise). The
 * tag is NOT loaded with the page: it starts after the visitor's first interaction, or after the browser has been idle for a few
 * seconds, whichever comes first, so it never competes with the content (docs/14, docs/17). The script is added by this nonce-
 * trusted code and `www.googletagmanager.com` is named in the CSP only while an id is configured (see lib/csp.ts).
 */
export function Analytics({ id }: { id: string }) {
  useEffect(() => {
    if (!GA4_ID.test(id)) return;
    let started = false;
    const events = ['pointerdown', 'keydown', 'scroll', 'touchstart'] as const;

    const start = () => {
      if (started) return;
      started = true;
      for (const name of events) window.removeEventListener(name, start);
      window.dataLayer = window.dataLayer ?? [];
      window.gtag = function gtag(...args: unknown[]) {
        window.dataLayer!.push(args);
      };
      window.gtag('js', new Date());
      window.gtag('config', id);
      const script = document.createElement('script');
      script.async = true;
      script.src = `https://www.googletagmanager.com/gtag/js?id=${encodeURIComponent(id)}`;
      document.head.append(script);
    };

    for (const name of events) window.addEventListener(name, start, { passive: true, once: true });
    const idle = typeof window.requestIdleCallback === 'function' ? window.requestIdleCallback(start, { timeout: 8000 }) : window.setTimeout(start, 8000);
    return () => {
      for (const name of events) window.removeEventListener(name, start);
      if (typeof window.cancelIdleCallback === 'function') window.cancelIdleCallback(idle as number);
      else window.clearTimeout(idle as number);
    };
  }, [id]);

  return null;
}
