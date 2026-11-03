'use client';

import { useState, type ChangeEvent } from 'react';
import { Alert } from '@/components/Alert';
import { Button } from '@/components/Button';
import { FileField } from '@/components/form';
import { useFieldIds } from '@/components/form/Form';
import { MAX_IMAGE_BYTES } from '@/lib/admin/image-upload';
import { siteMessages } from '@/lib/admin/site-messages';
import type { SiteImagePurpose, SiteUploadResult } from '@/lib/admin/site-types';
import { formatNumber } from '@/lib/format';
import styles from './site.module.css';

export interface UploadedImage {
  id: number;
  url: string;
  width: number;
  height: number;
}

interface Props {
  /** Name of the hidden input that carries the media id in the editor's form (empty = no image). */
  name: string;
  label: string;
  purpose: SiteImagePurpose;
  initial: UploadedImage | null;
  hint?: string;
  /** Show a «حذف تصویر» button (images that are optional). */
  removable?: boolean;
  /** The preview sits on the primary colour (light logos) instead of a neutral background. */
  onPrimary?: boolean;
  /** Called after an upload or a removal (editors that need the image, e.g. alt text defaults). */
  onChange?: (image: UploadedImage | null) => void;
}

const NARROW = 1200;

/**
 * Uploads the chosen file at once (same-origin route, streamed to the API) and keeps only the returned media id in the form.
 * The file input itself is cleared after the upload, so no image bytes travel with the editor's own Server Action.
 */
export function ImageUploadField({ name, label, purpose, initial, hint, removable, onPrimary, onChange }: Props) {
  const [image, setImage] = useState<UploadedImage | null>(initial);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null);
  const [narrow, setNarrow] = useState(false);
  // The API reports a media problem under the payload path (`logo.mediaId`), which is this field's name; the message goes
  // inline under the file input like every other field's, and the input is the one that takes focus.
  const { error } = useFieldIds(name, false);

  async function upload(event: ChangeEvent<HTMLDivElement>) {
    const input = event.target as HTMLInputElement;
    if (input.type !== 'file') return;
    const file = input.files?.[0];
    if (!file) return;
    if (file.size > MAX_IMAGE_BYTES) {
      setMessage({ ok: false, text: siteMessages.upload.tooBig });
      input.value = '';
      return;
    }
    setBusy(true);
    setMessage(null);
    const form = new FormData();
    form.set('file', file);
    form.set('purpose', purpose);
    try {
      const response = await fetch('/admin/api/site/images', { method: 'POST', body: form });
      const json = (await response.json().catch(() => null)) as { ok?: boolean; message?: string; image?: SiteUploadResult } | null;
      if (response.ok && json?.image) {
        const next = { id: json.image.id, url: json.image.url, width: json.image.width, height: json.image.height };
        setImage(next);
        setNarrow(purpose !== 'logo' && purpose !== 'favicon' && next.width < NARROW);
        setMessage({ ok: true, text: siteMessages.upload.uploaded });
        onChange?.(next);
      } else {
        setMessage({ ok: false, text: json?.message ?? siteMessages.upload.failed });
      }
    } catch {
      setMessage({ ok: false, text: siteMessages.upload.failed });
    } finally {
      setBusy(false);
      input.value = '';
    }
  }

  function remove() {
    setImage(null);
    setNarrow(false);
    setMessage(null);
    onChange?.(null);
  }

  return (
    <div className="ui-field ui-field--full">
      <input type="hidden" name={name} value={image?.id ?? ''} />
      <div onChange={upload}>
        <FileField name={`${name}File`} label={label} error={error} accept="image/jpeg,image/png,image/webp" hint={hint ? `${hint} ${siteMessages.upload.formats}` : siteMessages.upload.formats} />
      </div>
      <div className={styles.preview} aria-live="polite">
        {busy && <span className={styles.muted}>{siteMessages.upload.uploading}</span>}
        {image ? (
          <>
            {/* eslint-disable-next-line @next/next/no-img-element */}
            <img src={image.url} alt="" width={image.width} height={image.height} className={`${styles.previewImg} ${onPrimary ? '' : styles.previewImgLight}`} />
            <span className={styles.muted}>{siteMessages.upload.size(formatNumber(image.width), formatNumber(image.height))}</span>
            {removable && (
              <Button type="button" variant="secondary" onClick={remove}>
                {siteMessages.upload.remove}
              </Button>
            )}
          </>
        ) : (
          !busy && <span className={styles.muted}>{siteMessages.upload.none}</span>
        )}
        {message && <Alert variant={message.ok ? 'success' : 'danger'}>{message.text}</Alert>}
        {narrow && <Alert variant="warning">{siteMessages.upload.narrow}</Alert>}
      </div>
    </div>
  );
}
