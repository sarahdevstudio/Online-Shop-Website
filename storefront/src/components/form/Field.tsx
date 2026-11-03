'use client';

import type { ChangeEventHandler, HTMLInputAutoCompleteAttribute, ReactNode } from 'react';
import { formMessages } from '@/lib/form-messages';
import { useFieldIds } from './Form';

interface Shared {
  name: string;
  label: string;
  hint?: string;
  required?: boolean;
  /** Spans every column of the grid (long fields: address, description, text). */
  full?: boolean;
  disabled?: boolean;
  /** Keep the label for screen readers only (cells of a dense matrix). */
  hideLabel?: boolean;
  /** Explicit error text; normally the error comes from the surrounding <Form errors>. */
  error?: string;
}

function cls(...parts: Array<string | false | undefined>) {
  return parts.filter(Boolean).join(' ');
}

function FieldMessages({ id, hint, error }: { id: string; hint?: string; error?: string }) {
  return (
    <>
      {hint && (
        <p id={`${id}-hint`} className="ui-hint">
          {hint}
        </p>
      )}
      {error && (
        <p id={`${id}-error`} className="ui-error">
          <span className="ui-visually-hidden">{formMessages.errorPrefix} </span>
          {error}
        </p>
      )}
    </>
  );
}

function Label({ id, label, required, hidden }: { id: string; label: string; required?: boolean; hidden?: boolean }) {
  return (
    <label htmlFor={id} className={hidden ? 'ui-visually-hidden' : 'ui-label'}>
      {label}
      {required && (
        <span className="ui-required" aria-hidden="true">
          *
        </span>
      )}
    </label>
  );
}

interface TextProps extends Shared {
  type?: 'text' | 'password' | 'email' | 'tel' | 'search';
  /** Left-to-right content (prices, SKU, slug, card number, phone, postal code). */
  ltr?: boolean;
  inputMode?: 'numeric' | 'tel' | 'decimal' | 'email' | 'text' | 'search';
  autoComplete?: HTMLInputAutoCompleteAttribute;
  maxLength?: number;
  placeholder?: string;
  enterKeyHint?: 'search' | 'done' | 'go' | 'next' | 'send';
  defaultValue?: string | number | null;
  value?: string;
  onChange?: ChangeEventHandler<HTMLInputElement>;
  /** Short field (quantity) that keeps a fixed width in compact forms. */
  compact?: boolean;
}

export function TextField({ name, label, hint, required, full, disabled, hideLabel, error: errorProp, type = 'text', ltr, inputMode, autoComplete, maxLength, placeholder, enterKeyHint, defaultValue, value, onChange, compact }: TextProps) {
  const { id, error, describedBy, invalid } = useFieldIds(name, Boolean(hint), errorProp);
  return (
    <div className={cls('ui-field', full && 'ui-field--full', compact && 'ui-field--compact')}>
      <Label id={id} label={label} required={required} hidden={hideLabel} />
      <input
        id={id}
        name={name}
        type={type}
        className="ui-control"
        dir={ltr ? 'ltr' : undefined}
        inputMode={inputMode}
        autoComplete={autoComplete}
        maxLength={maxLength}
        placeholder={placeholder}
        enterKeyHint={enterKeyHint}
        required={required}
        disabled={disabled}
        aria-invalid={invalid}
        aria-describedby={describedBy}
        {...(value !== undefined ? { value, onChange } : { defaultValue: defaultValue ?? '' })}
      />
      <FieldMessages id={id} hint={hint} error={error} />
    </div>
  );
}

interface AreaProps extends Shared {
  rows?: number;
  autoComplete?: HTMLInputAutoCompleteAttribute;
  maxLength?: number;
  ltr?: boolean;
  /** Monospace (Markdown source). */
  code?: boolean;
  defaultValue?: string | null;
  value?: string;
  onChange?: ChangeEventHandler<HTMLTextAreaElement>;
}

export function TextAreaField({ name, label, hint, required, full = true, disabled, hideLabel, error: errorProp, rows = 4, autoComplete, maxLength, ltr, code, defaultValue, value, onChange }: AreaProps) {
  const { id, error, describedBy, invalid } = useFieldIds(name, Boolean(hint), errorProp);
  return (
    <div className={cls('ui-field', full && 'ui-field--full')}>
      <Label id={id} label={label} required={required} hidden={hideLabel} />
      <textarea
        id={id}
        name={name}
        rows={rows}
        autoComplete={autoComplete}
        className={cls('ui-control', code && 'ui-control--code')}
        dir={ltr ? 'ltr' : code ? 'auto' : undefined}
        maxLength={maxLength}
        required={required}
        disabled={disabled}
        aria-invalid={invalid}
        aria-describedby={describedBy}
        {...(value !== undefined ? { value, onChange } : { defaultValue: defaultValue ?? '' })}
      />
      <FieldMessages id={id} hint={hint} error={error} />
    </div>
  );
}

interface SelectProps extends Shared {
  defaultValue?: string | number | null;
  children: ReactNode;
}

export function SelectField({ name, label, hint, required, full, disabled, hideLabel, error: errorProp, defaultValue, children }: SelectProps) {
  const { id, error, describedBy, invalid } = useFieldIds(name, Boolean(hint), errorProp);
  return (
    <div className={cls('ui-field', full && 'ui-field--full')}>
      <Label id={id} label={label} required={required} hidden={hideLabel} />
      <select id={id} name={name} className="ui-control" defaultValue={defaultValue ?? ''} required={required} disabled={disabled} aria-invalid={invalid} aria-describedby={describedBy}>
        {children}
      </select>
      <FieldMessages id={id} hint={hint} error={error} />
    </div>
  );
}

interface CheckProps {
  name: string;
  label: string;
  hint?: string;
  defaultChecked?: boolean;
  value?: string;
  disabled?: boolean;
  full?: boolean;
  error?: string;
}

export function CheckField({ name, label, hint, defaultChecked, value, disabled, full, error: errorProp }: CheckProps) {
  const { id, error, describedBy, invalid } = useFieldIds(name, Boolean(hint), errorProp);
  return (
    <div className={cls('ui-field', full && 'ui-field--full')}>
      <label className="ui-check-row">
        <input id={id} type="checkbox" name={name} value={value} className="ui-check" defaultChecked={defaultChecked} disabled={disabled} aria-invalid={invalid} aria-describedby={describedBy} />
        <span>{label}</span>
      </label>
      <FieldMessages id={id} hint={hint} error={error} />
    </div>
  );
}

interface ChoiceOption {
  value: string;
  title: string;
  description?: string;
}

interface ChoiceProps {
  name: string;
  legend: string;
  options: readonly ChoiceOption[];
  defaultValue?: string;
  required?: boolean;
  error?: string;
  full?: boolean;
  hideLegend?: boolean;
}

/** Radio cards (payment method): one fieldset, a legend, the error under the group. */
export function ChoiceGroup({ name, legend, options, defaultValue, required, error: errorProp, full = true, hideLegend }: ChoiceProps) {
  const { id, error, describedBy, invalid } = useFieldIds(name, false, errorProp);
  return (
    <fieldset id={id} className={cls('ui-choice-group', 'ui-field', full && 'ui-field--full')} role="radiogroup" aria-invalid={invalid} aria-describedby={describedBy} aria-required={required || undefined}>
      <legend className={hideLegend ? 'ui-visually-hidden' : undefined}>
        {legend}
        {required && (
          <span className="ui-required" aria-hidden="true">
            *
          </span>
        )}
      </legend>
      {options.map((option) => (
        <label key={option.value} className="ui-choice">
          <input type="radio" name={name} value={option.value} className="ui-check" defaultChecked={defaultValue === option.value} required={required} />
          <span className="ui-choice__title">{option.title}</span>
          {option.description && <span className="ui-choice__desc">{option.description}</span>}
        </label>
      ))}
      <FieldMessages id={id} error={error} />
    </fieldset>
  );
}

interface FileProps {
  name: string;
  label: string;
  accept?: string;
  hint?: string;
  required?: boolean;
  full?: boolean;
  error?: string;
}

/** Native file picker with the shared field markup (label, required mark, hint, error). */
export function FileField({ name, label, accept, hint, required, full, error: errorProp }: FileProps) {
  const { id, error, describedBy, invalid } = useFieldIds(name, Boolean(hint), errorProp);
  return (
    <div className={cls('ui-field', full && 'ui-field--full')}>
      <Label id={id} label={label} required={required} />
      <input id={id} className="ui-control" type="file" name={name} accept={accept} required={required} aria-invalid={invalid} aria-describedby={describedBy} />
      <FieldMessages id={id} hint={hint} error={error} />
    </div>
  );
}
