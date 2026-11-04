import { describe, expect, it } from 'vitest';
import { uploadErrorMessage } from '../src/lib/admin/image-upload';
import { adminMessages } from '../src/lib/admin/messages';
import { camelKeys, problemState } from '../src/lib/admin/form-state';

// The API returns only texts from the translation table (the API guarantees it, tests there); the storefront shows them
// exactly as received, in any culture: an edited text is never filtered or replaced. Ours appear only when there is none.
describe('API text is shown as received', () => {
  it('shows an edited text in any language, as-is', () => {
    expect(camelKeys({ FullName: ['Please enter a name'] })).toEqual({ fullName: 'Please enter a name' });
    const state = problemState({ status: 500, code: null, detail: 'Something went wrong', errors: null });
    expect(state.message).toBe('Something went wrong');
  });

  it('uses its own text only when the API sent none', () => {
    expect(camelKeys({ FullName: [''] })).toEqual({ fullName: adminMessages.invalidValue });
    expect(problemState({ status: 500, code: null, detail: null, errors: null }).message).toBe(adminMessages.failed);
  });

  it('keeps a Persian detail', () => {
    const state = problemState({ status: 500, code: null, detail: 'تنوع محصول پیدا نشد.', errors: null });
    expect(state.message).toBe('تنوع محصول پیدا نشد.');
  });

  it('shows the API\'s own Persian message for a rejected file, and ours for the rest', () => {
    expect(uploadErrorMessage(400, { file: ['حجم تصویر باید بین ۱ بایت و 5242880 بایت باشد.'] })).toBe('حجم تصویر باید بین ۱ بایت و 5242880 بایت باشد.');
    expect(uploadErrorMessage(400, { file: ['The file is bad.'] })).toBe('The file is bad.');
    expect(uploadErrorMessage(400, null)).toBe(adminMessages.products.imageFailed);
    expect(uploadErrorMessage(413, null)).toBe(adminMessages.products.imageTooBig);
    expect(uploadErrorMessage(500, null)).toBe(adminMessages.products.imageFailed);
  });
});
