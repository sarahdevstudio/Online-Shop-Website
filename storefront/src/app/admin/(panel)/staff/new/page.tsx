import Link from 'next/link';
import { adminGet } from '@/lib/admin/api';
import { staffMessages } from '@/lib/admin/staff-messages';
import type { StaffOverview } from '@/lib/admin/staff-types';
import { CreateStaffForm } from '../StaffForms';

export default async function NewStaffPage() {
  const overview = await adminGet<StaffOverview>('/admin/staff');
  if (!overview) throw new Error('Staff management is unavailable.');
  return (
    <>
      <p>
        <Link href="/admin/staff">{staffMessages.back}</Link>
      </p>
      <h1 className="ui-heading-2">{staffMessages.newUser}</h1>
      <CreateStaffForm roles={overview.roles} />
    </>
  );
}
