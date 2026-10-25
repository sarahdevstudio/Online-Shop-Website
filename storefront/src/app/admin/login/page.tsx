import { redirect } from 'next/navigation';
import { readAdminSession } from '@/lib/admin/api';
import { adminMessages } from '@/lib/admin/messages';
import { LoginForm } from './LoginForm';
import styles from '../admin.module.css';

export default async function AdminLoginPage() {
  if (await readAdminSession()) redirect('/admin');
  return (
    <main className={styles.loginPage}>
      <h1 className="ui-heading-3">{adminMessages.loginTitle}</h1>
      <LoginForm />
    </main>
  );
}
