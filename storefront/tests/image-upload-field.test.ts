import { createElement } from 'react';
import { renderToStaticMarkup } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { Form } from '@/components/form';
import { ImageUploadField } from '@/app/admin/(panel)/site/_components/ImageUploadField';

function render(errors: Record<string, string>) {
  const field = createElement(ImageUploadField, { name: 'logo.mediaId', label: 'لوگو', purpose: 'logo', initial: null });
  return renderToStaticMarkup(createElement(Form, { errors, action: () => undefined }, field));
}

describe('ImageUploadField', () => {
  it('shows the API error for its media field inline, linked to the file input', () => {
    const html = render({ 'logo.mediaId': 'تصویر پیدا نشد.' });
    const input = html.match(/<input[^>]*type="file"[^>]*>/)?.[0] ?? '';
    expect(input).toContain('aria-invalid="true"');
    const errorId = input.match(/aria-describedby="[^"]*?([^\s"]+-error)"/)?.[1];
    expect(errorId).toBeTruthy();
    expect(html).toContain(`id="${errorId}"`);
    expect(html).toContain('تصویر پیدا نشد.');
    // inline copy plus the one in the summary at the top of the form
    expect(html.split('تصویر پیدا نشد.').length - 1).toBeGreaterThanOrEqual(2);
  });

  it('shows no error text when the field is valid', () => {
    const html = render({ 'other.mediaId': 'x' });
    expect(html).not.toContain('aria-invalid="true"');
    expect(html).not.toMatch(/ui-error/);
  });
});
