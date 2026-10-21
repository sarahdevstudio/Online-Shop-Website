import type { ReactNode } from 'react';
import styles from './Badge.module.css';

export type BadgeVariant = 'neutral' | 'primary' | 'sale' | 'success' | 'warning' | 'danger';

export function Badge({ variant = 'neutral', className, children }: { variant?: BadgeVariant; className?: string; children: ReactNode }) {
  return <span className={[styles.badge, styles[variant], className].filter(Boolean).join(' ')}>{children}</span>;
}
