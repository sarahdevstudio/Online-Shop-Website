'use client';

import { useEffect } from 'react';
import { messages } from '@/lib/messages';

/**
 * Shown when a page fails while rendering, e.g. the API is unreachable or answered 5xx. Next.js answers with 500
 * (an App Router page can't choose another status); the response is no-store and this page is noindex.
 */
export default function Error({ error, reset }: { error: Error & { digest?: string }; reset: () => void }) {
  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <div className="ui-container ui-section ui-stack">
      <meta name="robots" content="noindex" />
      <h1 className="ui-heading-2">{messages.unavailableTitle}</h1>
      <p>{messages.unavailableText}</p>
      <p>
        <button type="button" onClick={reset}>
          {messages.retry}
        </button>
      </p>
    </div>
  );
}
