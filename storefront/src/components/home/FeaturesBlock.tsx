import { Icon } from '@/components/Icon';
import type { HomeBlock } from '@/lib/site-config';
import styles from './FeaturesBlock.module.css';

type Data = Extract<HomeBlock, { type: 'features' }>['data'];

/** The trust/benefits strip: a short row of admin-edited icon + title + text items. */
export function FeaturesBlock({ data }: { data: Data }) {
  return (
    <section className="ui-container ui-section">
      <ul className={styles.list}>
        {data.items.map((item, i) => (
          <li key={i} className={styles.item}>
            <span className={styles.icon}>
              <Icon name={item.icon} />
            </span>
            <span>
              <strong className={styles.title}>{item.title}</strong>
              <span className={styles.text}>{item.text}</span>
            </span>
          </li>
        ))}
      </ul>
    </section>
  );
}
