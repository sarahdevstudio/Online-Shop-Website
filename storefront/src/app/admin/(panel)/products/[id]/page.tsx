import { notFound } from 'next/navigation';
import { adminGet } from '@/lib/admin/api';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminAttribute, AdminBrand, AdminCategory, AdminProduct, StockHistoryEntry } from '@/lib/admin/types';
import { AttributesEditor } from '../AttributesEditor';
import { ImagesEditor } from '../ImagesEditor';
import { EditProductForm } from '../ProductForms';
import { VariantsEditor } from '../VariantsEditor';
import styles from '../../../admin.module.css';

export default async function EditProductPage({ params }: { params: Promise<{ id: string }> }) {
  const id = Number((await params).id);
  if (!Number.isSafeInteger(id) || id <= 0) notFound();
  const product = await adminGet<AdminProduct>(`/admin/products/${id}`);
  if (!product) notFound();

  const [categories, brands, attributes, histories] = await Promise.all([
    adminGet<AdminCategory[]>('/admin/categories'),
    adminGet<AdminBrand[]>('/admin/brands'),
    adminGet<AdminAttribute[]>('/admin/attributes'),
    Promise.all(product.variants.map((v) => adminGet<StockHistoryEntry[]>(`/admin/variants/${v.id}/stock-history`))),
  ]);
  const history: Record<number, StockHistoryEntry[]> = {};
  product.variants.forEach((v, i) => (history[v.id] = histories[i] ?? []));

  return (
    <>
      <h1 className="ui-heading-2">{product.name}</h1>
      <section className={styles.panel} aria-labelledby="general">
        <h2 id="general" className="ui-heading-4">
          {adminMessages.edit}
        </h2>
        <EditProductForm product={product} categories={categories ?? []} brands={brands ?? []} />
      </section>
      <VariantsEditor productId={product.id} variants={product.variants} history={history} />
      <AttributesEditor product={product} attributes={attributes ?? []} />
      <ImagesEditor product={product} />
    </>
  );
}
