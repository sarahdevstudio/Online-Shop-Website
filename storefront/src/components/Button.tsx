'use client';

import Link from 'next/link';
import type { ButtonHTMLAttributes, ComponentProps, ReactNode } from 'react';
import styles from './Button.module.css';

type Variant = 'primary' | 'secondary' | 'ghost' | 'danger';
type Size = 'sm' | 'md' | 'lg';

interface Common {
  variant?: Variant;
  size?: Size;
  loading?: boolean;
  block?: boolean;
  iconOnly?: boolean;
  children: ReactNode;
}

function classes({ variant = 'primary', size = 'md', loading, block, iconOnly }: Common, extra?: string) {
  return [
    styles.btn,
    styles[variant],
    size !== 'md' ? styles[size] : '',
    block ? styles.block : '',
    iconOnly ? styles.icon : '',
    loading ? styles.loading : '',
    extra ?? '',
  ]
    .filter(Boolean)
    .join(' ');
}

/** Native <button>. `loading` shows a spinner, sets aria-busy and swallows clicks/submits. Icon-only needs aria-label. */
export function Button(props: Common & ButtonHTMLAttributes<HTMLButtonElement>) {
  const { variant, size, loading, block, iconOnly, children, className, onClick, ...rest } = props;
  return (
    <button
      {...rest}
      className={classes({ variant, size, loading, block, iconOnly, children }, className)}
      aria-busy={loading || undefined}
      onClick={(event) => {
        if (loading) {
          event.preventDefault();
          return;
        }
        onClick?.(event);
      }}
    >
      {loading && <span className={styles.spinner} aria-hidden="true" />}
      {children}
    </button>
  );
}

/** Link styled as a button (keeps real <a> semantics and client navigation). */
export function ButtonLink(props: Common & ComponentProps<typeof Link>) {
  const { variant, size, loading, block, iconOnly, children, className, ...rest } = props;
  return (
    <Link {...rest} className={classes({ variant, size, loading, block, iconOnly, children }, className)}>
      {children}
    </Link>
  );
}
