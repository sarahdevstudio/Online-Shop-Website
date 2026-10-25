'use client';

import { useEffect } from 'react';
import { adminMessages } from '@/lib/admin/messages';

/** An admin page failed to render (the API is down or answered an unexpected error). */
export default function AdminError({ error, reset }: { error: Error & { digest?: string }; reset: () => void }) {
  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <main style={{ padding: '2rem', maxWidth: '40rem', margin: '0 auto' }}>
      <h1 className="ui-heading-3">{adminMessages.failed}</h1>
      <p>
        <button type="button" className="ui-control" style={{ inlineSize: 'auto' }} onClick={reset}>
          {adminMessages.reload}
        </button>
      </p>
    </main>
  );
}
