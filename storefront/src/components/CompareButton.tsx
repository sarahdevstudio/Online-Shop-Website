import Link from 'next/link';
import { cookies } from 'next/headers';
import { toggleCompareAction } from '@/app/(shop)/compare/actions';
import { Form, SubmitButton } from '@/components/form';
import { COMPARE_COOKIE, COMPARE_MAX, parseCompare } from '@/lib/compare';
import { compareMessages as t } from '@/lib/compare-messages';
import { formatNumber } from '@/lib/format';

/**
 * "Add to compare" / "Remove from compare" on the product page (a plain form posting to a Server Action: no script needed).
 * The listing cards do not carry one, on purpose: a form per card would make every listing page heavier (docs/14).
 */
export async function CompareButton({ slug, returnTo }: { slug: string; returnTo: string }) {
  const list = parseCompare((await cookies()).get(COMPARE_COOKIE)?.value);
  const inList = list.includes(slug);
  const full = !inList && list.length >= COMPARE_MAX;
  return (
    <div className="ui-cluster">
      {full ? (
        <p className="ui-text-muted">{t.full(formatNumber(COMPARE_MAX))}</p>
      ) : (
        <Form action={toggleCompareAction} inline>
          <input type="hidden" name="slug" value={slug} />
          <input type="hidden" name="returnTo" value={returnTo} />
          <SubmitButton variant="secondary">{inList ? t.remove : t.add}</SubmitButton>
        </Form>
      )}
      {list.length > 0 && <Link href="/compare">{t.open(formatNumber(list.length))}</Link>}
    </div>
  );
}
