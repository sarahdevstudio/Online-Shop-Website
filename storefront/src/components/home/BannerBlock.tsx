import Image from 'next/image';
import Link from 'next/link';
import type { HomeBlock } from '@/lib/site-config';
import styles from './BannerBlock.module.css';

type Data = Extract<HomeBlock, { type: 'banner' }>['data'];

/** An image only (no overlaid text), optionally a link; `alt` is mandatory in the configuration. */
export function BannerBlock({ data }: { data: Data }) {
  const picture = (
    <span className={`${styles.frame} ${styles[data.height]}`}>
      <Image src={data.image.url} alt={data.alt} fill sizes="(min-width: 1280px) 80rem, 100vw" />
    </span>
  );
  return (
    <section className="ui-container ui-section">
      {data.link ? (
        /^https:/i.test(data.link) ? (
          <a className={styles.link} href={data.link} rel="noopener noreferrer nofollow" target="_blank">
            {picture}
          </a>
        ) : (
          <Link className={styles.link} href={data.link}>
            {picture}
          </Link>
        )
      ) : (
        picture
      )}
    </section>
  );
}
