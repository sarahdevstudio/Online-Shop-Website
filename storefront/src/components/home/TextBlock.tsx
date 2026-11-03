import { renderMarkdown } from '@/lib/markdown';
import type { HomeBlock } from '@/lib/site-config';

type Data = Extract<HomeBlock, { type: 'text' }>['data'];

export function TextBlock({ data, headingId }: { data: Data; headingId: string }) {
  return (
    <section className="ui-container ui-section" aria-labelledby={data.heading ? headingId : undefined}>
      {data.heading && (
        <h2 id={headingId} className="ui-heading-3">
          {data.heading}
        </h2>
      )}
      {/* Safe: raw HTML is disabled and only safe links survive (lib/markdown.ts), same as on the info pages. */}
      <div className="ui-prose" dangerouslySetInnerHTML={{ __html: renderMarkdown(data.body) }} />
    </section>
  );
}
