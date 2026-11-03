'use client';

import { useRouter } from 'next/navigation';
import { useActionState, useState, type FormEvent } from 'react';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { FileField, Form, FormActions, FormGrid, FormSection, SubmitButton, TextField } from '@/components/form';
import { INITIAL_FORM_STATE } from '@/lib/admin/form-state';
import { MAX_IMAGE_BYTES } from '@/lib/admin/image-upload';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminProduct } from '@/lib/admin/types';
import { FormBanner } from '../../_components/FormBits';
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
      <FormActions end={
        <Form action={remove} inline>
          <input type="hidden" name="productId" value={productId} />
          <input type="hidden" name="imageId" value={image.id} />
          <SubmitButton variant="danger">{adminMessages.delete}</SubmitButton>
        </Form>
      }>
        {index > 0 && (
          <Form action={move} inline>
            <input type="hidden" name="productId" value={productId} />
            <input type="hidden" name="order" value={swapped(index, index - 1)} />
            <SubmitButton variant="secondary">{t.moveUp}</SubmitButton>
          </Form>
        )}
        {index < order.length - 1 && (
          <Form action={move} inline>
            <input type="hidden" name="productId" value={productId} />
            <input type="hidden" name="order" value={swapped(index, index + 1)} />
            <SubmitButton variant="secondary">{t.moveDown}</SubmitButton>
          </Form>
        )}
      </FormActions>
    </li>
  );
}

export function ImagesEditor({ product }: { product: AdminProduct }) {
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<{ ok: boolean; message: string } | null>(null);
  const [fileError, setFileError] = useState<string | undefined>();
  const order = product.images.map((i) => i.id);

  async function upload(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const file = (form.elements.namedItem('file') as HTMLInputElement).files?.[0];
    if (!file) {
      setFileError(t.imageRequired);
      return;
    }
    if (file.size > MAX_IMAGE_BYTES) {
      setFileError(t.imageTooBig);
      return;
    }
    setFileError(undefined);
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
    <div id="images">
      <FormSection card title={t.images}>
        {product.images.length === 0 ? (
          <p className={styles.muted}>{t.noImages}</p>
        ) : (
          <ul className={styles.thumbs}>
            {product.images.map((image, index) => (
              <ImageRow key={image.id} productId={product.id} image={image} order={order} index={index} />
            ))}
          </ul>
        )}
        <Form onSubmit={upload} encType="multipart/form-data" errors={{ file: fileError }} banner={result ? <Alert variant={result.ok ? 'success' : 'danger'}>{result.message}</Alert> : null}>
          <FormGrid>
            <FileField name="file" label={t.upload} accept="image/jpeg,image/png,image/webp" required full />
            <TextField name="alt" label={t.alt} maxLength={200} autoComplete="off" full />
          </FormGrid>
          <FormActions>
            <Button type="submit" loading={busy}>
              {busy ? t.uploading : t.upload}
            </Button>
          </FormActions>
        </Form>
      </FormSection>
    </div>
  );
}
