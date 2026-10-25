'use client';

import { useRouter } from 'next/navigation';
import { useActionState, useState, type FormEvent } from 'react';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { INITIAL_FORM_STATE } from '@/lib/admin/form-state';
import { MAX_IMAGE_BYTES } from '@/lib/admin/image-upload';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminProduct } from '@/lib/admin/types';
import { FormBanner, SubmitButton } from '../../_components/FormBits';
import { deleteImageAction, reorderImagesAction } from './actions';
import styles from '../../admin.module.css';

const t = adminMessages.products;

function ImageRow({ productId, image, order, index }: { productId: number; image: AdminProduct['images'][number]; order: number[]; index: number }) {
  const [moveState, move] = useActionState(reorderImagesAction, INITIAL_FORM_STATE);
  const [deleteState, remove] = useActionState(deleteImageAction, INITIAL_FORM_STATE);
  const swapped = (from: number, to: number) => {
    const next = [...order];
    [next[from], next[to]] = [next[to], next[from]];
    return next.join(',');
  };
  return (
    <li className={styles.thumb}>
      {/* eslint-disable-next-line @next/next/no-img-element */}
      <img src={image.url} alt={image.alt} />
      <span className={styles.muted}>{image.alt}</span>
      <FormBanner state={moveState} />
      <FormBanner state={deleteState} />
      <div className={styles.row}>
        {index > 0 && (
          <form action={move}>
            <input type="hidden" name="productId" value={productId} />
            <input type="hidden" name="order" value={swapped(index, index - 1)} />
            <SubmitButton variant="secondary" size="sm">
              {t.moveUp}
            </SubmitButton>
          </form>
        )}
        {index < order.length - 1 && (
          <form action={move}>
            <input type="hidden" name="productId" value={productId} />
            <input type="hidden" name="order" value={swapped(index, index + 1)} />
            <SubmitButton variant="secondary" size="sm">
              {t.moveDown}
            </SubmitButton>
          </form>
        )}
        <form action={remove}>
          <input type="hidden" name="productId" value={productId} />
          <input type="hidden" name="imageId" value={image.id} />
          <SubmitButton variant="danger" size="sm">
            {adminMessages.delete}
          </SubmitButton>
        </form>
      </div>
    </li>
  );
}

export function ImagesEditor({ product }: { product: AdminProduct }) {
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<{ ok: boolean; message: string } | null>(null);
  const order = product.images.map((i) => i.id);

  async function upload(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const file = (form.elements.namedItem('file') as HTMLInputElement).files?.[0];
    if (!file) return;
    if (file.size > MAX_IMAGE_BYTES) {
      setResult({ ok: false, message: t.imageTooBig });
      return;
    }
    setBusy(true);
    setResult(null);
    try {
      // Same-origin route handler that streams the body to the API (no Server Action size limit involved).
      const response = await fetch(`/admin/api/products/${product.id}/images`, { method: 'POST', body: new FormData(form) });
      const json = (await response.json().catch(() => null)) as { ok?: boolean; message?: string } | null;
      setResult({ ok: response.ok, message: json?.message ?? (response.ok ? t.imageUploaded : t.imageFailed) });
      if (response.ok) {
        form.reset();
        router.refresh();
      }
    } catch {
      setResult({ ok: false, message: t.imageFailed });
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className={styles.panel} aria-labelledby="images">
      <h2 id="images" className="ui-heading-4">
        {t.images}
      </h2>
      {product.images.length === 0 ? (
        <p className={styles.muted}>{t.noImages}</p>
      ) : (
        <ul className={styles.thumbs}>
          {product.images.map((image, index) => (
            <ImageRow key={image.id} productId={product.id} image={image} order={order} index={index} />
          ))}
        </ul>
      )}
      <form onSubmit={upload} className={styles.row} encType="multipart/form-data">
        <label className={styles.field}>
          <span className={styles.fieldLabel}>{t.upload}</span>
          <input className="ui-control" type="file" name="file" accept="image/jpeg,image/png,image/webp" required />
        </label>
        <label className={styles.field}>
          <span className={styles.fieldLabel}>{t.alt}</span>
          <input className="ui-control" type="text" name="alt" maxLength={200} />
        </label>
        <Button type="submit" loading={busy}>
          {busy ? t.uploading : t.upload}
        </Button>
      </form>
      {result && <Alert variant={result.ok ? 'success' : 'danger'}>{result.message}</Alert>}
    </section>
  );
}
