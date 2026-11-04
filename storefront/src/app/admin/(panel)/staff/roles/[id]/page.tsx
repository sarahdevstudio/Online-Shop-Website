import Link from 'next/link';
import { notFound } from 'next/navigation';
import { adminGet } from '@/lib/admin/api';
import { staffMessages } from '@/lib/admin/staff-messages';
import type { StaffOverview } from '@/lib/admin/staff-types';
import { DeleteRoleForm, RoleForm } from '../RoleForms';

export default async function EditRolePage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const overview = await adminGet<StaffOverview>('/admin/staff');
  if (!overview) throw new Error('Staff management is unavailable.');
  const role = overview.roles.find((r) => r.id === id);
  if (!role || role.isSystem) notFound();
  return (
    <>
      <p>
        <Link href="/admin/staff/roles">{staffMessages.backRoles}</Link>
      </p>
      <h1 className="ui-heading-2">{role.title ?? role.name}</h1>
      <RoleForm role={role} permissions={overview.permissions} />
      <DeleteRoleForm role={role} />
    </>
  );
}
