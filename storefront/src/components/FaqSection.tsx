import { JsonLd } from '@/components/JsonLd';
import { messages } from '@/lib/messages';
import { faqJsonLd } from '@/lib/seo';

/** The visible questions and answers plus the FAQPage structured data built from the same items, so both always agree. */
export function FaqSection({ items }: { items: readonly { question: string; answer: string }[] }) {
  const data = faqJsonLd(items);
  if (!data) return null;
  return (
    <section aria-labelledby="faq" className="ui-stack">
      <JsonLd data={data} />
      <h2 id="faq" className="ui-heading-3">
        {messages.faqTitle}
      </h2>
      <dl className="ui-stack">
        {items.map((item) => (
          <div key={item.question}>
            <dt>
              <strong>{item.question}</strong>
            </dt>
            <dd>{item.answer}</dd>
          </div>
        ))}
      </dl>
    </section>
  );
}
