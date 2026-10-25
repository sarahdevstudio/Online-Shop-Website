/** DTOs of `/api/v1/store/*` (see docs/07-checkout-design.md). Enums arrive as camelCase strings. */

export interface CartLineDto {
  variantId: number;
  productName: string;
  productSlug: string;
  variantLabel: string | null;
  sku: string;
  imageUrl: string | null;
  unitPrice: number;
  quantity: number;
  maxQuantity: number;
  lineTotal: number;
  available: boolean;
  notice: 'unavailable' | 'quantity_exceeds' | 'price_changed' | null;
  previousPrice: number | null;
}

export interface CartDto {
  lines: CartLineDto[];
  itemCount: number;
  subtotal: number;
  shippingFee: number;
  total: number;
  freeShippingThreshold: number | null;
  hasBlockingIssue: boolean;
  paymentMethods: { cardToCard: boolean; cashOnDelivery: boolean };
}

export interface CartMutationDto {
  newToken: string | null;
  cart: CartDto;
  clamped: boolean;
}

export type PaymentMethod = 'cardToCard' | 'cashOnDelivery';
export type OrderStatus = 'awaitingPayment' | 'pending' | 'confirmed' | 'shipped' | 'delivered' | 'cancelled';
export type PaymentStatus = 'unpaid' | 'paid';

export interface PlacedOrderDto {
  accessToken: string;
  number: number;
  replayed: boolean;
}

export interface OrderDto {
  number: number;
  status: OrderStatus;
  paymentStatus: PaymentStatus;
  paymentMethod: PaymentMethod;
  createdAtUtc: string;
  fullName: string;
  mobile: string;
  province: string;
  city: string;
  address: string;
  postalCode: string;
  note: string | null;
  lines: { productName: string; variantLabel: string | null; sku: string; unitPrice: number; quantity: number; lineTotal: number }[];
  subtotal: number;
  shippingFee: number;
  total: number;
  paymentInstructions: { cardNumber: string; holderName: string; amount: number } | null;
  transferReference: { tracking: string; time: string; cardLast4: string } | null;
}

export interface ShortageLine {
  variantId: number;
  productName: string;
  variantLabel: string | null;
  requested: number;
  available: number;
}

/** RFC 7807 problem from the API, reduced to what the UI uses. */
export interface Problem {
  status: number;
  code: string | null;
  detail: string | null;
  errors: Record<string, string[]> | null;
  lines: ShortageLine[] | null;
}
