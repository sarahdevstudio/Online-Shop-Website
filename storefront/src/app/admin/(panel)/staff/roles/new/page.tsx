import Link from 'next/link';
import { adminGet } from '@/lib/admin/api';
import { staffMessages } from '@/lib/admin/staff-messages';
import type { StaffOverview } from '@/lib/admin/staff-types';
import { RoleForm } from '../RoleForms';

export default async function NewRolePage() {
  const overview = await adminGet<StaffOverview>('/admin/staff');
  if (!overview) throw new Error('Staff management is unavailable.');
  return (
    <>
      <p>
        <Link href="/admin/staff/roles">{staffMessages.backRoles}</Link>
      </p>
      <h1 className="ui-heading-2">{staffMessages.newRole}</h1>
      <RoleForm permissions={overview.permissions} />
    </>
  );
}
