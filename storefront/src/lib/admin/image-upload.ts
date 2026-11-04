import { adminMessages } from './messages';

/** The API's `Media:MaxImageBytes` default (5 MB). The API is the authority; these only give fast, clear feedback. */
export const MAX_IMAGE_BYTES = 5 * 1024 * 1024;

/** A multipart body is the file plus boundaries and headers: refuse only what cannot possibly contain an allowed image. */
export const MULTIPART_OVERHEAD_BYTES = 64 * 1024;

export function requestTooLarge(contentLength: number | null): boolean {
  return contentLength !== null && contentLength > MAX_IMAGE_BYTES + MULTIPART_OVERHEAD_BYTES;
}

/** The message for a rejected upload: the API's text for the file (validation), or ours for a 413 / anything else. */
export function uploadErrorMessage(status: number, errors: Record<string, string[]> | null): string {
  if (status === 413) return adminMessages.products.imageTooBig;
  // The API's own message for the file (size, type...) is the accurate one and is editable in the translation editor.
  const file = (errors?.file ?? errors?.File ?? [])[0];
  if (status === 400 && file) return file;
  return adminMessages.products.imageFailed;
}
