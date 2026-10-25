import { adminGet } from '@/lib/admin/api';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminCategory } from '@/lib/admin/types';
import { CategoryForm, DeleteCategoryForm } from './CategoryForms';
import styles from '../../admin.module.css';

export default async function CategoriesPage() {
  const categories = (await adminGet<AdminCategory[]>('/admin/categories')) ?? [];
  const roots = categories.filter((c) => c.parentId === null);
  return (
    <>
      <h1 className="ui-heading-2">{adminMessages.taxonomy.categories}</h1>
      <section className={styles.panel} aria-labelledby="new">
        <h2 id="new" className="ui-heading-4">
          {adminMessages.taxonomy.newCategory}
        </h2>
        <CategoryForm roots={roots} />
      </section>

      <p className={styles.hint}>{adminMessages.taxonomy.deleteHint}</p>
      {categories.map((category) => (
        <details key={`${category.id}-${category.name}-${category.sortOrder}-${category.isActive}-${category.parentId}`} className={styles.panel}>
          <summary>
            {category.parentId !== null && '↳ '}
            {category.name} <span className={`${styles.muted} ${styles.ltr}`}>{category.slug}</span>
            {!category.isActive && <span className={styles.badge}> {adminMessages.no}</span>}
          </summary>
          <CategoryForm category={category} roots={roots} />
          <DeleteCategoryForm id={category.id} />
        </details>
      ))}
    </>
  );
}
