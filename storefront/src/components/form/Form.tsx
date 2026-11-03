'use client';

import { createContext, useContext, useEffect, useId, useRef, type FormHTMLAttributes, type ReactNode } from 'react';
import { formMessages } from '@/lib/form-messages';

export type FormErrors = Readonly<Record<string, string | undefined>>;

const NO_ERRORS: FormErrors = {};

interface FormContextValue {
  prefix: string;
  errors: FormErrors;
}

const FormContext = createContext<FormContextValue>({ prefix: 'f', errors: NO_ERRORS });

/** Id, error text and `aria-*` wiring of one field, from the surrounding <Form>. */
export function useFieldIds(name: string, hasHint: boolean, errorOverride?: string) {
  const { prefix, errors } = useContext(FormContext);
  const id = `${prefix}-${name}`;
  const error = errorOverride ?? errors[name];
  const describedBy = [hasHint ? `${id}-hint` : null, error ? `${id}-error` : null].filter(Boolean).join(' ') || undefined;
  return { id, error, describedBy, invalid: error ? (true as const) : undefined };
}

const FOCUSABLE = 'input:not([type=hidden]),select,textarea,button';

interface FormProps extends Omit<FormHTMLAttributes<HTMLFormElement>, 'noValidate'> {
  /** field name → message, from the server action's state. Shown under each field and in the summary at the top. */
  errors?: FormErrors;
  /** Result banner (success / failure / conflict); shown above the error summary. */
  banner?: ReactNode;
  /** A compact one-row form (filters, quantity). */
  inline?: boolean;
  summaryHeading?: string;
}

/**
 * The one <form> of the site. Browser validation is off (its bubbles are not Persian and not styled): the server's Persian
 * messages appear under each field, an error summary appears at the top and focus moves to the first invalid field.
 */
export function Form({ errors = NO_ERRORS, banner, inline, summaryHeading = formMessages.fixErrors, className, children, ...rest }: FormProps) {
  const prefix = useId();
  const ref = useRef<HTMLFormElement>(null);
  const entries = Object.entries(errors).filter((entry): entry is [string, string] => Boolean(entry[1]));

  useEffect(() => {
    if (entries.length === 0) return;
    const form = ref.current;
    if (!form) return;
    const first = form.querySelector<HTMLElement>('[aria-invalid="true"]');
    const target = first?.matches(FOCUSABLE) ? first : (first?.querySelector<HTMLElement>(FOCUSABLE) ?? form.querySelector<HTMLElement>('[data-form-summary]'));
    target?.focus();
    // eslint-disable-next-line react-hooks/exhaustive-deps -- one focus move per new error set
  }, [errors]);

  return (
    <FormContext.Provider value={{ prefix, errors }}>
      <form ref={ref} noValidate className={`ui-form ${inline ? 'ui-form--inline' : ''} ${className ?? ''}`.trim()} {...rest}>
        {banner}
        {entries.length > 0 && (
          <div className="ui-form-summary" role="alert" tabIndex={-1} data-form-summary>
            <p className="ui-form-summary__title">{summaryHeading}</p>
            <ul>
              {entries.map(([name, message]) => (
                <li key={name}>
                  <a
                    href={`#${prefix}-${name}`}
                    onClick={(event) => {
                      const el = document.getElementById(`${prefix}-${name}`);
                      if (!el) return;
                      event.preventDefault();
                      (el.matches(FOCUSABLE) ? el : el.querySelector<HTMLElement>(FOCUSABLE))?.focus();
                    }}
                  >
                    {message}
                  </a>
                </li>
              ))}
            </ul>
          </div>
        )}
        {children}
      </form>
    </FormContext.Provider>
  );
}
