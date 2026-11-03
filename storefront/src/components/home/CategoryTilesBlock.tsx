import Image from 'next/image';
import Link from 'next/link';
import { Icon } from '@/components/Icon';
import { messages } from '@/lib/messages';
import type { HomeBlock } from '@/lib/site-config';
import styles from './CategoryTilesBlock.module.css';

type Data = Extract<HomeBlock, { type: 'categoryTiles' }>['data'];

export function CategoryTilesBlock({ data, headingId }: { data: Data; headingId: string }) {
  return (
    <section className="ui-container ui-section" aria-labelledby={headingId}>
      <h2 id={headingId} className="ui-heading-3">
        {messages.categories}
      </h2>
      <ul className={styles.tiles}>
        {data.items.map((item) => (
          <li key={item.slug}>
            <Link className={styles.tile} href={`/category/${item.slug}`}>
              <span className={styles.media}>
                {item.imageUrl ? <Image src={item.imageUrl} alt="" fill sizes="(min-width: 1024px) 16rem, 50vw" /> : <Icon name="image" />}
              </span>
              <span className={styles.label}>{item.label}</span>
            </Link>
          </li>
        ))}
      </ul>
    </section>
  );
}
