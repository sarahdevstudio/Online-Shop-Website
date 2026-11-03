import { messages } from '@/lib/messages';
import styles from './PreviewBanner.module.css';

/** Fixed notice on every previewed page; the link ends the preview and returns to the admin. */
export function PreviewBanner() {
  return (
    <div className={styles.banner} role="status">
      <div className={`ui-container ${styles.inner}`}>
        <strong>{messages.previewBanner}</strong>
        {/* A plain GET that clears the cookie (a Link would prefetch it). */}
        <a href="/api/site-preview/stop?next=/admin/site">{messages.previewBack}</a>
      </div>
    </div>
  );
}
