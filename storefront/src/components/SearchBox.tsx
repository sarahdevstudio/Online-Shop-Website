'use client';

import { useRouter } from 'next/navigation';
import { useEffect, useId, useRef, useState, type FormEvent, type KeyboardEvent } from 'react';
import { formatNumber } from '@/lib/format';
import { messages } from '@/lib/messages';
import { suggestMessages as t } from '@/lib/suggest-messages';
import { Icon } from './Icon';
import styles from './SearchBox.module.css';

interface Suggestions {
  products: { slug: string; name: string; path: string; price: number; imageUrl: string | null }[];
  categories: { slug: string; name: string; path: string }[];
  fuzzy: boolean;
}

type Option = { id: string; label: string; href: string; group: 'products' | 'categories' | 'all'; detail?: string };

const DEBOUNCE_MS = 150;
const MIN_CHARS = 2;

/**
 * The header search box with suggestions: an ARIA 1.2 combobox with a listbox of products and categories (typo tolerant, from the
 * API's index). Keyboard: Down/Up move through the options (Down opens the list), Home/End jump, Enter opens the active option or, with
 * none active, searches for what is typed, Escape closes the list (a second Escape clears the text), Tab leaves. Screen readers hear
 * the number of suggestions. Without JavaScript it is a plain form that submits to /search.
 */
export function SearchBox({ className }: { className?: string }) {
  const router = useRouter();
  const base = useId();
  const listId = `${base}-list`;
  const [value, setValue] = useState('');
  const [data, setData] = useState<Suggestions | null>(null);
  const [open, setOpen] = useState(false);
  const [active, setActive] = useState(-1);
  const controller = useRef<AbortController | null>(null);

  const query = value.trim();
  const options: Option[] = [];
  if (data) {
    data.products.forEach((p, i) => options.push({ id: `${base}-p${i}`, label: p.name, href: p.path, group: 'products', detail: t.price(formatNumber(p.price)) }));
    data.categories.forEach((c, i) => options.push({ id: `${base}-c${i}`, label: c.name, href: c.path, group: 'categories' }));
  }
  if (query.length >= MIN_CHARS) options.push({ id: `${base}-all`, label: t.allResults(query), href: `/search?q=${encodeURIComponent(query)}`, group: 'all' });
  const suggestionCount = options.filter((o) => o.group !== 'all').length;

  useEffect(() => {
    if (query.length < MIN_CHARS) {
      controller.current?.abort();
      setData(null);
      setOpen(false);
      return;
    }
    const timer = window.setTimeout(async () => {
      controller.current?.abort();
      const own = new AbortController();
      controller.current = own;
      try {
        const response = await fetch(`/api/suggest?q=${encodeURIComponent(query)}`, { signal: own.signal });
        if (!response.ok) return;
        setData((await response.json()) as Suggestions);
        setActive(-1);
        setOpen(true);
      } catch {
        // aborted or offline: keep what is shown
      }
    }, DEBOUNCE_MS);
    return () => window.clearTimeout(timer);
  }, [query]);

  function go(href: string) {
    setOpen(false);
    router.push(href);
  }

  function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (active >= 0 && options[active]) return go(options[active].href);
    if (query) go(`/search?q=${encodeURIComponent(query)}`);
  }

  function onKeyDown(event: KeyboardEvent<HTMLInputElement>) {
    const last = options.length - 1;
    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        if (!open && options.length > 0) setOpen(true);
        setActive((index) => (index >= last ? 0 : index + 1));
        break;
      case 'ArrowUp':
        event.preventDefault();
        if (!open && options.length > 0) setOpen(true);
        setActive((index) => (index <= 0 ? last : index - 1));
        break;
      case 'Home':
        if (open && options.length > 0) {
          event.preventDefault();
          setActive(0);
        }
        break;
      case 'End':
        if (open && options.length > 0) {
          event.preventDefault();
          setActive(last);
        }
        break;
      case 'Escape':
        if (open) {
          event.preventDefault();
          setOpen(false);
          setActive(-1);
        } else if (value) {
          event.preventDefault();
          setValue('');
        }
        break;
      case 'Tab':
        setOpen(false);
        break;
    }
  }

  const expanded = open && options.length > 0;
  const groups: { key: Option['group']; title: string | null }[] = [
    { key: 'products', title: t.products },
    { key: 'categories', title: t.categories },
    { key: 'all', title: null },
  ];

  return (
    <form className={`${styles.root} ${className ?? ''}`} role="search" onSubmit={onSubmit}>
      <div className="ui-field">
        <label className="ui-visually-hidden" htmlFor={`${base}-input`}>
          {messages.searchLabel}
        </label>
        <input
          id={`${base}-input`}
          name="q"
          type="text"
          className="ui-control"
          role="combobox"
          aria-expanded={expanded}
          aria-controls={listId}
          aria-autocomplete="list"
          aria-activedescendant={expanded && active >= 0 ? options[active]?.id : undefined}
          autoComplete="off"
          autoCapitalize="off"
          spellCheck={false}
          enterKeyHint="search"
          placeholder={messages.searchPlaceholder}
          value={value}
          onChange={(event) => setValue(event.target.value)}
          onKeyDown={onKeyDown}
          onBlur={() => setOpen(false)}
          onFocus={() => options.length > 0 && query.length >= MIN_CHARS && setOpen(true)}
        />
      </div>
      <button type="submit" className={styles.submit} aria-label={messages.searchSubmit}>
        <Icon name="search" />
      </button>

      <ul id={listId} role="listbox" aria-label={t.products} className={styles.list} hidden={!expanded}>
        {data?.fuzzy && (
          <li role="presentation" className={styles.note}>
            {t.nearest}
          </li>
        )}
        {groups.map(({ key, title }) => {
          const items = options.filter((o) => o.group === key);
          if (items.length === 0) return null;
          return (
            <li key={key} role="presentation">
              {title && (
                <span role="presentation" className={styles.groupTitle}>
                  {title}
                </span>
              )}
              <ul role="group" aria-label={title ?? t.allResults(query)} className={styles.group}>
                {items.map((option) => {
                  const index = options.indexOf(option);
                  return (
                    <li
                      key={option.id}
                      id={option.id}
                      role="option"
                      aria-selected={index === active}
                      className={`${styles.option} ${index === active ? styles.activeOption : ''}`}
                      // mousedown (not click): the input must not lose focus first, or the list would close before the click lands
                      onMouseDown={(event) => {
                        event.preventDefault();
                        go(option.href);
                      }}
                      onMouseMove={() => setActive(index)}
                    >
                      <span>{option.label}</span>
                      {option.detail && <span className={styles.detail}>{option.detail}</span>}
                    </li>
                  );
                })}
              </ul>
            </li>
          );
        })}
      </ul>

      {/* What a screen reader hears while typing: how many suggestions there are. */}
      <p role="status" aria-live="polite" className="ui-visually-hidden">
        {expanded ? (suggestionCount > 0 ? t.count(formatNumber(suggestionCount)) : t.none) : ''}
      </p>
    </form>
  );
}
