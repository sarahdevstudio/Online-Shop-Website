import { apiGet } from './api';

/** The approved reviews of a product (public: no mobile number) and their average. */
export interface ProductReviews {
  count: number;
  average: number;
  items: { name: string; rating: number; text: string; createdAtUtc: string }[];
}

export const NO_REVIEWS: ProductReviews = { count: 0, average: 0, items: [] };

/** Reviews never take a product page down: any trouble means "no reviews shown". */
export async function getProductReviews(slug: string): Promise<ProductReviews> {
  try {
    const dto = await apiGet<ProductReviews>(`/catalog/reviews/${encodeURIComponent(slug)}`, 60);
    return dto && dto.count > 0 ? dto : NO_REVIEWS;
  } catch {
    return NO_REVIEWS;
  }
}
