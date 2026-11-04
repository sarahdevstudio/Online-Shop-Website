import Link from 'next/link';
import { transferMessages as t, TRANSFER_COLUMNS } from '@/lib/admin/transfer-messages';
import { TransferForm } from './TransferForm';
import styles from '../../../admin.module.css';

export default function TransferPage() {
  return (
    <>
      <p>
        <Link href="/admin/products">{t.back}</Link>
      </p>
      <h1 className="ui-heading-2">{t.title}</h1>
      <p>{t.intro}</p>

      <section className={styles.panel} aria-labelledby="export">
        <h2 id="export" className="ui-heading-4">
          {t.exportTitle}
        </h2>
        <p>{t.exportHint}</p>
        <p>
          {/* Plain links to the download route (no JavaScript needed). */}
          <a href="/admin/api/catalog/export?format=csv" className="ui-control" style={{ inlineSize: 'auto', textDecoration: 'none' }} download>
            {t.exportCsv}
          </a>{' '}
          <a href="/admin/api/catalog/export?format=xlsx" className="ui-control" style={{ inlineSize: 'auto', textDecoration: 'none' }} download>
            {t.exportXlsx}
          </a>
        </p>
      </section>

      <TransferForm />

      <section className={styles.panel} aria-labelledby="format">
        <h2 id="format" className="ui-heading-4">
          {t.formatTitle}
        </h2>
        <p>{t.formatIntro}</p>
        <dl>
          {TRANSFER_COLUMNS.map((column) => (
            <div key={column}>
              <dt>
                <bdi dir="ltr">
                  <code>{column}</code>
                </bdi>
              </dt>
              <dd>{t.columnHelp(column)}</dd>
            </div>
          ))}
          <div>
            <dt>
              <bdi dir="ltr">
                <code>spec:… / axis:…</code>
              </bdi>
            </dt>
            <dd>{t.columnHelp('attributes')}</dd>
          </div>
        </dl>
        <p className="ui-hint">{t.kept}</p>
      </section>
    </>
  );
}
