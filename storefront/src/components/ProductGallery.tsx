import Image from 'next/image';
import { Icon } from './Icon';
import styles from './ProductGallery.module.css';

interface GalleryImage {
  url: string;
  alt: string;
}

/** Main image (the LCP element, loaded with priority) plus thumbnails; a placeholder when there are no images. */
export function ProductGallery({ images, name }: { images: readonly GalleryImage[]; name: string }) {
  if (images.length === 0) {
    return (
      <div className={styles.main} role="img" aria-label={name}>
        <Icon name="image" className={styles.placeholder} />
      </div>
    );
  }

  const [first, ...rest] = images;
  return (
    <div className={styles.gallery}>
      <div className={styles.main}>
        <Image src={first.url} alt={first.alt} width={720} height={720} sizes="(min-width: 1024px) 45vw, 100vw" priority />
      </div>
      {rest.length > 0 && (
        <ul className={styles.thumbs}>
          {rest.map((image) => (
            <li key={image.url}>
              <Image src={image.url} alt={image.alt} width={160} height={160} sizes="160px" />
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
