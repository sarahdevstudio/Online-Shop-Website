import { notFound } from 'next/navigation';
import { adminGet } from '@/lib/admin/api';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminDiscount, DiscountAuditEntry } from '@/lib/admin/types';
import { formatJalaliDate } from '@/lib/format';
import { DeleteDiscountForm, DiscountEditor } from '../DiscountEditor';
import styles from '../../../admin.module.css';

const t = adminMessages.discounts;

export default async function EditDiscountPage({ params }: { params: Promise<{ id: string }> }) {
  const raw = (await params).id;
  if (!/^[1-9]\d{0,9}$/.test(raw)) notFound();
  const [discount, audit] = await Promise.all([
    adminGet<AdminDiscount>(`/admin/discounts/${raw}`),
    adminGet<DiscountAuditEntry[]>(`/admin/discounts/${raw}/audit`),
  ]);
  if (!discount) notFound();

  return (
    <>
      <h1 className="ui-heading-2">
        <bdi dir="ltr">{discount.code}</bdi>
      </h1>
      <DiscountEditor discount={discount} />

      <section className={styles.panel} aria-labelledby="history">
        <h2 id="history" className="ui-heading-4">
          {t.history}
        </h2>
        {(audit ?? []).length === 0 ? (
          <p className={styles.muted}>{t.noHistory}</p>
        ) : (
          <ul>
            {(audit ?? []).map((entry, i) => (
              <li key={i}>
                {formatJalaliDate(entry.atUtc)} — <bdi dir="ltr">{entry.adminUsername ?? adminMessages.none}</bdi> — {t.actions[entry.action] ?? adminMessages.none}
              </li>
            ))}
          </ul>
        )}
      </section>

      <DeleteDiscountForm id={discount.id} />
    </>
  );
}
