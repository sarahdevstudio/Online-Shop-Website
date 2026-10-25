import { adminGet } from '@/lib/admin/api';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminBrand, AdminCategory } from '@/lib/admin/types';
import { NewProductForm } from '../ProductForms';

export default async function NewProductPage() {
  const [categories, brands] = await Promise.all([
    adminGet<AdminCategory[]>('/admin/categories'),
    adminGet<AdminBrand[]>('/admin/brands'),
  ]);
  return (
    <>
      <h1 className="ui-heading-2">{adminMessages.products.new}</h1>
      <NewProductForm categories={categories ?? []} brands={brands ?? []} />
    </>
  );
}
