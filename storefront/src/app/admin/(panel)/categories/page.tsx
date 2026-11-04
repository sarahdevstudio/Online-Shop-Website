import { SeoPanel } from '../../_components/SeoPanel';
import type { AdminSeo } from '@/lib/admin/seo-types';
import { adminGet } from '@/lib/admin/api';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminCategory, RouteTarget } from '@/lib/admin/types';
import { CategoryForm, DeleteCategoryForm } from './CategoryForms';
import styles from '../../admin.module.css';

export default async function CategoriesPage() {
  const categories = (await adminGet<AdminCategory[]>('/admin/categories')) ?? [];
  const roots = categories.filter((c) => c.parentId === null);
  const routes = new Map((await Promise.all(categories.map((c) => adminGet<RouteTarget>(`/admin/routes/category/${c.id}`)))).flatMap((r) => (r ? [[r.id, r] as const] : [])));
  const seos = new Map((await Promise.all(categories.map((c) => adminGet<AdminSeo>(`/admin/seo/category/${c.id}`)))).flatMap((r) => (r ? [[r.id, r] as const] : [])));
  return (
    <>
      <h1 className="ui-heading-2">{adminMessages.taxonomy.categories}</h1>
      <CategoryForm roots={roots} title={adminMessages.taxonomy.newCategory} />

      <p className={styles.hint}>{adminMessages.taxonomy.deleteHint}</p>
      {categories.map((category) => (
        <details key={`${category.id}-${category.name}-${category.sortOrder}-${category.isActive}-${category.parentId}`} className={styles.panel}>
          <summary>
            {category.parentId !== null && '↳ '}
            {category.name} <span className={`${styles.muted} ${styles.ltr}`}>{category.slug}</span>
            {!category.isActive && <span className={styles.badge}> {adminMessages.no}</span>}
          </summary>
          <CategoryForm category={category} roots={roots} route={routes.get(category.id)} />
          {seos.get(category.id) && <SeoPanel data={seos.get(category.id)!} />}
          <DeleteCategoryForm id={category.id} />
        </details>
      ))}
    </>
  );
}
