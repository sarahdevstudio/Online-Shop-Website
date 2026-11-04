import Link from 'next/link';
import { notFound } from 'next/navigation';
import { adminGet, requireAdmin } from '@/lib/admin/api';
import { staffMessages } from '@/lib/admin/staff-messages';
import type { StaffOverview } from '@/lib/admin/staff-types';
import { ActiveForm, ProfileForm, ResetPasswordForm, UserRolesForm } from '../StaffForms';

export default async function StaffUserPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const [session, overview] = await Promise.all([requireAdmin(), adminGet<StaffOverview>('/admin/staff')]);
  if (!overview) throw new Error('Staff management is unavailable.');
  const user = overview.users.find((u) => u.id === id);
  if (!user) notFound();
  return (
    <>
      <p>
        <Link href="/admin/staff">{staffMessages.back}</Link>
      </p>
      <h1 className="ui-heading-2">
        <bdi dir="ltr">{user.username}</bdi>
      </h1>
      <ProfileForm user={user} />
      <UserRolesForm user={user} roles={overview.roles} />
      <ActiveForm user={user} isSelf={user.username === session.username} />
      <ResetPasswordForm user={user} />
    </>
  );
}
