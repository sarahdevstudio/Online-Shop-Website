import { requireAdmin } from '@/lib/admin/api';
import { adminMessages } from '@/lib/admin/messages';
import { ChangePasswordForm } from './ChangePasswordForm';

export default async function AccountPage() {
  const session = await requireAdmin();
  return (
    <>
      <h1 className="ui-heading-2">{adminMessages.nav.account}</h1>
      <p dir="ltr">{session.username}</p>
      <ChangePasswordForm />
    </>
  );
}
