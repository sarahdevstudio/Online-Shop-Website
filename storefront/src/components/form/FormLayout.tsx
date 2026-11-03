import type { ReactNode } from 'react';
import { formMessages } from '@/lib/form-messages';

/** A titled block of a form. `card` draws the bordered panel used for the big admin forms. */
export function FormSection({ title, hint, card, children }: { title?: string; hint?: string; card?: boolean; children: ReactNode }) {
  return (
    <section className={`ui-form-section ${card ? 'ui-form-section--card' : ''}`}>
      {title && <h2 className="ui-form-section__title">{title}</h2>}
      {hint && <p className="ui-form-section__hint">{hint}</p>}
      {children}
    </section>
  );
}

/** 1 column on phones, 2 on desktop (3 with `columns={3}`; `pair` = always 2, for min/max). Fields with `full` span every column. */
export function FormGrid({ columns = 2, children }: { columns?: 2 | 3 | 'pair'; children: ReactNode }) {
  const modifier = columns === 3 ? 'ui-form-grid--3' : columns === 'pair' ? 'ui-form-grid--pair' : '';
  return <div className={`ui-form-grid ${modifier}`}>{children}</div>;
}

/** "Fields marked * are required": once at the top of a form that has required fields. */
export function RequiredNote() {
  return <p className="ui-form-note">{formMessages.requiredNote}</p>;
}

/**
 * The buttons of a form in one fixed order: primary first (children), then secondary; `end` (a destructive action) goes to
 * the far edge on desktop and last on phones. Phones stack them full width.
 */
export function FormActions({ children, end, stretch }: { children: ReactNode; end?: ReactNode; stretch?: boolean }) {
  return (
    <div className={`ui-form-actions ${stretch ? 'ui-form-actions--stretch' : ''}`}>
      {children}
      {end && <div className="ui-form-actions__end">{end}</div>}
    </div>
  );
}
