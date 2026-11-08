import { ReviewForm } from '@/app/(shop)/reviews/ReviewForm';
import { Icon } from '@/components/Icon';
import { formatJalaliDate, formatNumber } from '@/lib/format';
import { reviewMessages as t } from '@/lib/review-messages';
import type { ProductReviews } from '@/lib/reviews';

/** Five stars, filled up to the rating; the number is the accessible text, the stars are decoration. */
function Stars({ rating }: { rating: number }) {
  return (
    <span role="img" aria-label={t.stars(formatNumber(Math.round(rating * 10) / 10))} style={{ display: 'inline-flex', gap: '0.1rem', color: 'var(--color-warning, #b8860b)' }}>
      {[1, 2, 3, 4, 5].map((n) => (
        <span key={n} aria-hidden="true" style={{ opacity: n <= Math.round(rating) ? 1 : 0.25 }}>
          <Icon name="star" />
        </span>
      ))}
    </span>
  );
}

/**
 * Approved reviews and the form for a new one. Only what a moderator approved appears; the average is over those alone; with none
 * the section says so and shows no rating at all.
 */
export function ReviewsSection({ productSlug, reviews }: { productSlug: string; reviews: ProductReviews }) {
  return (
    <section aria-labelledby="reviews" className="ui-stack">
      <h2 id="reviews" className="ui-heading-3">
        {t.title}
      </h2>
      {reviews.count > 0 ? (
        <>
          <p>
            <Stars rating={reviews.average} /> {t.summary(formatNumber(reviews.average), formatNumber(reviews.count))}
          </p>
          <ul className="ui-stack" style={{ listStyle: 'none', padding: 0 }}>
            {reviews.items.map((review, index) => (
              <li key={`${review.createdAtUtc}-${index}`}>
                <p>
                  <strong>{review.name}</strong> <Stars rating={review.rating} />{' '}
                  <time dateTime={review.createdAtUtc.slice(0, 10)} className="ui-text-muted">
                    {formatJalaliDate(review.createdAtUtc.endsWith('Z') ? review.createdAtUtc : `${review.createdAtUtc}Z`)}
                  </time>
                </p>
                <p>{review.text}</p>
              </li>
            ))}
          </ul>
        </>
      ) : (
        <p className="ui-text-muted">{t.none}</p>
      )}
      <details>
        <summary>{t.write}</summary>
        <ReviewForm productSlug={productSlug} />
      </details>
    </section>
  );
}
