import { adminMessages } from './messages';

/** The API's `Media:MaxImageBytes` default (5 MB). The API is the authority; these only give fast, clear feedback. */
export const MAX_IMAGE_BYTES = 5 * 1024 * 1024;

/** A multipart body is the file plus boundaries and headers: refuse only what cannot possibly contain an allowed image. */
export const MULTIPART_OVERHEAD_BYTES = 64 * 1024;

export function requestTooLarge(contentLength: number | null): boolean {
  return contentLength !== null && contentLength > MAX_IMAGE_BYTES + MULTIPART_OVERHEAD_BYTES;
}

/** A clear Persian message for a rejected upload (API validation on `file`, or a 413). */
export function uploadErrorMessage(status: number, errors: Record<string, string[]> | null): string {
  if (status === 413) return adminMessages.products.imageTooBig;
  const file = (errors?.file ?? errors?.File ?? []).join(' ');
  if (status === 400 && /between 1 byte/i.test(file)) return adminMessages.products.imageTooBig;
  if (status === 400 && /JPEG, PNG and WebP/i.test(file)) return adminMessages.products.imageBadType;
  return adminMessages.products.imageFailed;
}
