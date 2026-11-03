/**
 * Product statuses of the admin list. The URL and the form use the lower-case names; the API takes the enum names
 * (`Draft`, `Published`, `Archived`) and is case-sensitive on purpose, so the storefront converts at the edge.
 */
export const PRODUCT_STATUSES = ['draft', 'published', 'archived'] as const;
export type ProductStatusName = (typeof PRODUCT_STATUSES)[number];

/** A known status from the query string, else '' (= all). */
export function parseProductStatus(value: string | undefined): ProductStatusName | '' {
  return (PRODUCT_STATUSES as readonly string[]).includes(value ?? '') ? (value as ProductStatusName) : '';
}

/** `published` → `Published`: the value the admin API expects. */
export function apiProductStatus(status: ProductStatusName): string {
  return status.charAt(0).toUpperCase() + status.slice(1);
}
