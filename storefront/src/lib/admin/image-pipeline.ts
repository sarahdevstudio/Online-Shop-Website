import sharp from 'sharp';

/** Long edge cap for every processed raster image; never upscale a smaller source. */
export const MAX_LONG_EDGE = 1600;
/** Product photos below this on their short edge are rejected (too small to be useful on a product page). */
export const MIN_PRODUCT_SHORT_EDGE = 600;
export const WEBP_QUALITY = 80;

export class ImageTooSmallError extends Error {
  constructor(public readonly minPx: number) {
    super(`image short edge below ${minPx}px`);
  }
}

/**
 * Product photo pipeline: auto-rotate from EXIF, strip all metadata (sharp strips it by default unless
 * `withMetadata()` is called), resize so the long edge is at most {@link MAX_LONG_EDGE} (never upscaled),
 * convert to WebP. Rejects a source below {@link MIN_PRODUCT_SHORT_EDGE} on its short edge.
 */
export async function processProductImage(input: Buffer): Promise<Buffer> {
  // min(width, height) is the short edge regardless of EXIF orientation: swapping the two labels doesn't change
  // which of the two numbers is smaller.
  const { width, height } = await sharp(input, { failOn: 'none' }).metadata();
  if (Math.min(width ?? 0, height ?? 0) < MIN_PRODUCT_SHORT_EDGE) {
    throw new ImageTooSmallError(MIN_PRODUCT_SHORT_EDGE);
  }
  return sharp(input, { failOn: 'none' })
    .rotate()
    .resize({ width: MAX_LONG_EDGE, height: MAX_LONG_EDGE, fit: 'inside', withoutEnlargement: true })
    .webp({ quality: WEBP_QUALITY })
    .toBuffer();
}

/** Same rotate/strip/resize as the product pipeline, for site hero/banner/share photos (no minimum-size rejection). */
export async function processSitePhoto(input: Buffer): Promise<Buffer> {
  return sharp(input, { failOn: 'none' })
    .rotate()
    .resize({ width: MAX_LONG_EDGE, height: MAX_LONG_EDGE, fit: 'inside', withoutEnlargement: true })
    .webp({ quality: WEBP_QUALITY })
    .toBuffer();
}

/**
 * Logo pipeline: rotate/strip/resize like a photo, but a logo with transparency stays lossless PNG (WebP's lossy
 * mode would fringe the alpha edge); a logo without transparency is small enough that WebP is still a clear win.
 */
export async function processLogo(input: Buffer): Promise<Buffer> {
  const metadata = await sharp(input, { failOn: 'none' }).metadata();
  const base = sharp(input, { failOn: 'none' })
    .rotate()
    .resize({ width: MAX_LONG_EDGE, height: MAX_LONG_EDGE, fit: 'inside', withoutEnlargement: true });
  return metadata.hasAlpha ? base.png({ compressionLevel: 9 }).toBuffer() : base.webp({ quality: WEBP_QUALITY }).toBuffer();
}
