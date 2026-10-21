import type { ReactNode } from 'react';
import { Icon } from './Icon';
import type { IconName } from '@/lib/icons';
import styles from './Alert.module.css';

export type AlertVariant = 'info' | 'success' | 'warning' | 'danger';

const ICON_BY_VARIANT: Record<AlertVariant, IconName> = { info: 'info', success: 'check', warning: 'alert', danger: 'alert' };

/** danger/warning interrupt screen readers (role=alert); info/success are polite (role=status). */
export function Alert({ variant = 'info', heading, children }: { variant?: AlertVariant; heading?: string; children: ReactNode }) {
  const role = variant === 'danger' || variant === 'warning' ? 'alert' : 'status';
  return (
    <div className={`${styles.alert} ${styles[variant]}`} role={role}>
      <Icon name={ICON_BY_VARIANT[variant]} className={styles.icon} />
      <div className={styles.body}>
        {heading && <strong className={styles.heading}>{heading}</strong>}
        <div>{children}</div>
      </div>
    </div>
  );
}
