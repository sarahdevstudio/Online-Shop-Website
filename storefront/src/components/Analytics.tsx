'use client';

import { useEffect } from 'react';
import { GA4_ID } from '@/lib/seo-patterns';

declare global {
  interface Window {
    dataLayer?: unknown[];
    gtag?: (...args: unknown[]) => void;
  }
}

/**
 * Google Analytics 4, only when the owner entered a measurement id (the layout does not render this component otherwise). The
 * tag is NOT loaded with the page: it starts after the visitor's first interaction, or, if they only read, a few seconds after the
 * page has finished loading and the browser is idle, whichever comes first, so it never competes with the content (docs/14, docs/17). The script is added by this nonce-
 * trusted code and `www.googletagmanager.com` is named in the CSP only while an id is configured (see lib/csp.ts).
 */
/** Quiet time after the page has loaded before the tag starts on its own. */
const IDLE_DELAY_MS = 4000;

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
    // No interaction: wait for the load event plus a few seconds (a quiet page is "idle" the moment it is hydrated, which is too early).
    let timer: number | undefined;
    let idle: number | undefined;
    const schedule = () => {
      timer = window.setTimeout(() => {
        if (typeof window.requestIdleCallback === 'function') idle = window.requestIdleCallback(start, { timeout: 5000 });
        else start();
      }, IDLE_DELAY_MS);
    };
    if (document.readyState === 'complete') schedule();
    else window.addEventListener('load', schedule, { once: true });

    return () => {
      for (const name of events) window.removeEventListener(name, start);
      window.removeEventListener('load', schedule);
      if (timer !== undefined) window.clearTimeout(timer);
      if (idle !== undefined && typeof window.cancelIdleCallback === 'function') window.cancelIdleCallback(idle);
    };
  }, [id]);

  return null;
}
