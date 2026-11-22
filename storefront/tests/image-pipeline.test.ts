import sharp from 'sharp';
import { describe, expect, it } from 'vitest';
import { ImageTooSmallError, MAX_LONG_EDGE, MIN_PRODUCT_SHORT_EDGE, processProductImage } from '../src/lib/admin/image-pipeline';

async function makeJpeg(width: number, height: number, withExif?: { orientation: number }): Promise<Buffer> {
  const base = sharp({ create: { width, height, channels: 3, background: { r: 200, g: 60, b: 60 } } });
  if (withExif) {
    return base.withExif({ IFD0: { Orientation: String(withExif.orientation) } }).jpeg().toBuffer();
  }
  return base.jpeg().toBuffer();
}

describe('processProductImage', () => {
  it('converts to WebP and caps the long edge without upscaling', async () => {
    const input = await makeJpeg(2000, 1000);
    const out = await processProductImage(input);
    const meta = await sharp(out).metadata();
    expect(meta.format).toBe('webp');
    expect(meta.width).toBe(MAX_LONG_EDGE);
    expect(meta.height).toBe(Math.round((1000 / 2000) * MAX_LONG_EDGE));
  });

  it('never upscales a smaller-than-cap source', async () => {
    const input = await makeJpeg(900, 700);
    const out = await processProductImage(input);
    const meta = await sharp(out).metadata();
    expect(meta.width).toBe(900);
    expect(meta.height).toBe(700);
  });

  it('strips metadata (EXIF) from the output', async () => {
    const input = await makeJpeg(900, 900, { orientation: 1 });
    const out = await processProductImage(input);
    const meta = await sharp(out).metadata();
    expect(meta.exif).toBeUndefined();
  });

  it('auto-rotates: an EXIF-rotated source carries no orientation tag in the output (already applied, not just stripped)', async () => {
    const input = await makeJpeg(1200, 900, { orientation: 6 });
    const out = await processProductImage(input);
    const meta = await sharp(out).metadata();
    expect(meta.orientation).toBeUndefined();
  });

  it('rejects an image below the minimum short edge', async () => {
    const input = await makeJpeg(500, 1000);
    await expect(processProductImage(input)).rejects.toBeInstanceOf(ImageTooSmallError);
  });

  it('accepts an image exactly at the minimum short edge', async () => {
    const input = await makeJpeg(MIN_PRODUCT_SHORT_EDGE, 900);
    await expect(processProductImage(input)).resolves.toBeInstanceOf(Buffer);
  });
});
