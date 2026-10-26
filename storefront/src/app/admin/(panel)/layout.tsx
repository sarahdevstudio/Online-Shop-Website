import type { ReactNode } from 'react';
import { requireAdmin } from '@/lib/admin/api';
import { adminMessages } from '@/lib/admin/messages';
import { logoutAction } from '../actions';
import { Nav } from '../_components/Nav';
import { SubmitButton } from '../_components/FormBits';
import styles from '../admin.module.css';

/** Everything under /admin except the login page: signed-in admins only (checked here and again in every action). */
export default async function PanelLayout({ children }: { children: ReactNode }) {
  const session = await requireAdmin();
  return (
    <div className={styles.shell}>
      <header className={styles.topbar}>
        <strong className={styles.brand}>{adminMessages.title}</strong>
        <Nav />
        <div className={styles.who}>
          <bdi dir="ltr">{session.username}</bdi>
          <form action={logoutAction}>
            <SubmitButton variant="secondary" size="sm">
              {adminMessages.logout}
            </SubmitButton>
          </form>
        </div>
      </header>
      <main id="main" className={styles.content} tabIndex={-1}>
        {children}
      </main>
    </div>
  );
}
