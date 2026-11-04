/** Shapes of /api/v1/admin/staff (docs/16). `stamp` is the version a form was loaded with: a save over a newer one is a 409. */
export interface StaffUser {
  id: string;
  username: string;
  displayName: string | null;
  email: string | null;
  isActive: boolean;
  roles: string[];
  createdAtUtc: string;
  stamp: string;
}

export interface StaffRole {
  id: string;
  name: string;
  title: string | null;
  isSystem: boolean;
  permissions: string[];
  userCount: number;
  stamp: string;
}

export interface StaffOverview {
  users: StaffUser[];
  roles: StaffRole[];
  permissions: string[];
}

export interface StaffAuditEntry {
  id: number;
  atUtc: string;
  actorUserId: string | null;
  actorName: string | null;
  action: string;
  subject: string | null;
  detail: string | null;
}

export interface StaffAuditPage {
  items: StaffAuditEntry[];
  total: number;
}

/** The text shown for a role: the built-in two have fixed names, a custom one shows its own title. */
export function roleLabel(role: Pick<StaffRole, 'name' | 'title'>, names: { admin: string; customer: string }): string {
  if (role.name === 'Admin') return names.admin;
  if (role.name === 'Customer') return names.customer;
  return role.title ?? role.name;
}
