import { Alert } from '@/components/Alert';
import { redirect } from 'next/navigation';
import { readAdminSession } from '@/lib/admin/api';
import { adminMessages } from '@/lib/admin/messages';
import { LoginForm } from './LoginForm';
import styles from '../admin.module.css';

export default async function AdminLoginPage({ searchParams }: { searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const ended = (await searchParams).ended === '1';
  if (!ended && (await readAdminSession())) redirect('/admin');
  return (
    <main className={styles.loginPage}>
      <h1 className="ui-heading-3">{adminMessages.loginTitle}</h1>
      {ended && <Alert variant="warning">{adminMessages.sessionEnded}</Alert>}
      <LoginForm />
    </main>
  );
}
