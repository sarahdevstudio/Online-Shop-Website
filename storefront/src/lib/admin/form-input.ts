import { parseLocalizedInteger } from '../format';

/** Reading form fields: everything arrives as a string, possibly in Persian digits. */

export const text = (formData: FormData, name: string): string => {
  const value = formData.get(name);
  return typeof value === 'string' ? value.trim() : '';
};

export const checked = (formData: FormData, name: string): boolean => formData.get(name) === 'on';

/** Whole number, or null for empty / not a number. */
export function whole(formData: FormData, name: string): number | null {
  const raw = text(formData, name);
  return raw === '' ? null : parseLocalizedInteger(raw);
}

/** Positive id from a hidden field, or null. */
export function id(formData: FormData, name = 'id'): number | null {
  const value = whole(formData, name);
  return value !== null && Number.isSafeInteger(value) && value > 0 ? value : null;
}
