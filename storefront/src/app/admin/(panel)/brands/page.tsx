import { adminGet } from '@/lib/admin/api';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminBrand } from '@/lib/admin/types';
import { BrandForm, DeleteBrandForm } from './BrandForms';
import styles from '../../admin.module.css';

export default async function BrandsPage() {
  const brands = (await adminGet<AdminBrand[]>('/admin/brands')) ?? [];
  return (
    <>
      <h1 className="ui-heading-2">{adminMessages.taxonomy.brands}</h1>
      <section className={styles.panel} aria-labelledby="new">
        <h2 id="new" className="ui-heading-4">
          {adminMessages.taxonomy.newBrand}
        </h2>
        <BrandForm />
      </section>

      <p className={styles.hint}>{adminMessages.taxonomy.deleteHint}</p>
      {brands.map((brand) => (
        <details key={`${brand.id}-${brand.name}-${brand.isActive}`} className={styles.panel}>
          <summary>
            {brand.name} <span className={`${styles.muted} ${styles.ltr}`}>{brand.slug}</span>
            {!brand.isActive && <span className={styles.badge}> {adminMessages.no}</span>}
          </summary>
          <BrandForm brand={brand} />
          <DeleteBrandForm id={brand.id} />
        </details>
      ))}
    </>
  );
}
