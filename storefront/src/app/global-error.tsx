'use client';

import { messages } from '@/lib/messages';

/** The root layout itself failed (it reads the category tree from the API), so this renders its own document. */
export default function GlobalError({ reset }: { error: Error & { digest?: string }; reset: () => void }) {
  return (
    <html lang="fa" dir="rtl">
      <body style={{ fontFamily: 'system-ui, sans-serif', maxWidth: '40rem', margin: '4rem auto', padding: '0 1rem' }}>
        <meta name="robots" content="noindex" />
        <h1>{messages.unavailableTitle}</h1>
        <p>{messages.unavailableText}</p>
        <button type="button" onClick={reset}>
          {messages.retry}
        </button>
      </body>
    </html>
  );
}
