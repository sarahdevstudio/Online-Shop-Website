import { describe, expect, it } from 'vitest';
import { persianOr } from '../src/lib/persian-only';
import { uploadErrorMessage } from '../src/lib/admin/image-upload';
import { adminMessages } from '../src/lib/admin/messages';
import { camelKeys, problemState } from '../src/lib/admin/form-state';

describe('persianOr', () => {
  it('keeps Persian text, also with a short Latin mention', () => {
    expect(persianOr('نام کاربری درست نیست.', 'x')).toBe('نام کاربری درست نیست.');
    expect(persianOr('فقط تصویرهای JPEG، PNG و WebP پذیرفته می‌شوند.', 'x')).toBe('فقط تصویرهای JPEG، PNG و WebP پذیرفته می‌شوند.');
  });

  it('replaces English, empty and missing text with the fallback', () => {
    expect(persianOr('Not Found', 'fallback')).toBe('fallback');
    expect(persianOr('The username must be unique, please try again', 'fallback')).toBe('fallback');
    expect(persianOr('', 'fallback')).toBe('fallback');
    expect(persianOr(null, 'fallback')).toBe('fallback');
    expect(persianOr(undefined, 'fallback')).toBe('fallback');
  });
});

describe('admin error text', () => {
  it('never shows an English field error or detail', () => {
    expect(camelKeys({ FullName: ["'Full Name' must not be empty."] })).toEqual({ fullName: adminMessages.invalidValue });
    const state = problemState({ status: 500, code: null, detail: 'An unexpected error occurred.', errors: null });
    expect(state.message).toBe(adminMessages.failed);
  });

  it('keeps a Persian detail', () => {
    const state = problemState({ status: 500, code: null, detail: 'تنوع محصول پیدا نشد.', errors: null });
    expect(state.message).toBe('تنوع محصول پیدا نشد.');
  });

  it('shows the API\'s own Persian message for a rejected file, and ours for the rest', () => {
    expect(uploadErrorMessage(400, { file: ['حجم تصویر باید بین ۱ بایت و 5242880 بایت باشد.'] })).toBe('حجم تصویر باید بین ۱ بایت و 5242880 بایت باشد.');
    expect(uploadErrorMessage(400, { file: ['The file is bad.'] })).toBe(adminMessages.products.imageFailed);
    expect(uploadErrorMessage(413, null)).toBe(adminMessages.products.imageTooBig);
    expect(uploadErrorMessage(500, null)).toBe(adminMessages.products.imageFailed);
  });
});
