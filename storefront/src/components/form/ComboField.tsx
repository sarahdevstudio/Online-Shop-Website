'use client';

import { useEffect, useId, useRef, useState, type KeyboardEvent } from 'react';
import { formMessages } from '@/lib/form-messages';
import { useFieldIds } from './Form';

export interface ComboOption {
  value: string;
  label: string;
  /** Second line: where the option leads, a code, ... */
  detail?: string;
}

interface ComboProps {
  name: string;
  label: string;
  hint?: string;
  required?: boolean;
  full?: boolean;
  disabled?: boolean;
  error?: string;
  /** Text of the current choice, shown in the box when it is not being edited. */
  selectedLabel: string;
  /** Looks options up; called on open and, after a short pause, on every change of the text. */
  search: (query: string) => Promise<readonly ComboOption[]>;
  onSelect: (option: ComboOption) => void;
  placeholder?: string;
  /** Texts for the live region (kept outside the component so the form system stays free of copy). */
  messages: { searching: string; noResults: string; results: (count: string) => string };
  formatCount?: (n: number) => string;
}

const PAUSE_MS = 250;

/**
 * A searchable select (ARIA 1.2 combobox with a list box). Typing narrows the list from the server, arrow keys move, Enter picks,
 * Escape closes. The text box shows the current choice until the user types. It only calls back: the surrounding editor owns the value.
 */
export function ComboField({ name, label, hint, required, full, disabled, error: errorProp, selectedLabel, search, onSelect, placeholder, messages, formatCount = String }: ComboProps) {
  const { id, error, describedBy, invalid } = useFieldIds(name, Boolean(hint), errorProp);
  const listId = `${useId()}-list`;
  const [query, setQuery] = useState('');
  const [editing, setEditing] = useState(false);
  const [open, setOpen] = useState(false);
  const [options, setOptions] = useState<readonly ComboOption[]>([]);
  const [loading, setLoading] = useState(false);
  const [active, setActive] = useState(-1);
  const requestRef = useRef(0);
  const timerRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

  useEffect(() => () => clearTimeout(timerRef.current), []);

  function run(text: string, immediately: boolean) {
    clearTimeout(timerRef.current);
    const request = ++requestRef.current;
    setLoading(true);
    timerRef.current = setTimeout(
      async () => {
        let found: readonly ComboOption[] = [];
        try {
          found = await search(text);
        } catch {
          found = [];
        }
        if (request !== requestRef.current) return; // a newer search is in flight
        setOptions(found);
        setActive(found.length > 0 ? 0 : -1);
        setLoading(false);
      },
      immediately ? 0 : PAUSE_MS,
    );
  }

  function openList() {
    if (disabled || open) return;
    setOpen(true);
    run(query, true);
  }

  function close() {
    requestRef.current++;
    clearTimeout(timerRef.current);
    setOpen(false);
    setEditing(false);
    setQuery('');
    setLoading(false);
  }

  function pick(option: ComboOption) {
    onSelect(option);
    close();
  }

  function onKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      if (!open) return openList();
      if (options.length === 0) return;
      const step = event.key === 'ArrowDown' ? 1 : -1;
      setActive((active + step + options.length) % options.length);
    } else if (event.key === 'Enter' && open) {
      event.preventDefault(); // never submits the editor form
      const option = options[active];
      if (option) pick(option);
    } else if (event.key === 'Escape' && open) {
      event.preventDefault();
      close();
    }
  }

  const status = loading ? messages.searching : options.length === 0 ? messages.noResults : messages.results(formatCount(options.length));

  return (
    <div className={`ui-field ui-combo${full ? ' ui-field--full' : ''}`}>
      <label htmlFor={id} className="ui-label">
        {label}
        {required && (
          <span className="ui-required" aria-hidden="true">
            *
          </span>
        )}
      </label>
      <input
        id={id}
        name={name}
        type="text"
        role="combobox"
        className="ui-control"
        aria-expanded={open}
        aria-controls={listId}
        aria-autocomplete="list"
        aria-activedescendant={open && active >= 0 ? `${listId}-${active}` : undefined}
        aria-invalid={invalid}
        aria-describedby={describedBy}
        autoComplete="off"
        placeholder={placeholder}
        disabled={disabled}
        value={editing ? query : selectedLabel}
        onFocus={openList}
        onClick={openList}
        onBlur={close}
        onChange={(event) => {
          setEditing(true);
          setQuery(event.target.value);
          setOpen(true);
          run(event.target.value, false);
        }}
        onKeyDown={onKeyDown}
      />
      {open && (
        <ul id={listId} role="listbox" aria-label={label} className="ui-combo__list">
          {options.map((option, i) => (
            <li
              key={option.value}
              id={`${listId}-${i}`}
              role="option"
              aria-selected={i === active}
              className={`ui-combo__option${i === active ? ' ui-combo__option--active' : ''}`}
              onMouseDown={(event) => event.preventDefault() /* keep the focus in the box so blur does not close the list first */}
              onClick={() => pick(option)}
              onMouseMove={() => setActive(i)}
            >
              <span>{option.label}</span>
              {option.detail && (
                <span className="ui-combo__detail" dir="ltr">
                  {option.detail}
                </span>
              )}
            </li>
          ))}
        </ul>
      )}
      <p className="ui-visually-hidden" role="status" aria-live="polite">
        {open ? status : ''}
      </p>
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
    </div>
  );
}
