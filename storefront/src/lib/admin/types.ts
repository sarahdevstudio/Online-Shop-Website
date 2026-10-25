import type { OrderDto, OrderStatus, PaymentMethod, PaymentStatus } from '../store-types';

/** DTOs of `/api/v1/admin/*` (enums are camelCase strings). */

export interface PagedList<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}

export interface Dashboard {
  newOrders: number;
  transferReferenceWaiting: number;
  confirmedToShip: number;
  ordersToday: number;
  lowStockVariants: number;
  lowStockThreshold: number;
}

export interface AdminOrderListItem {
  id: number;
  number: number;
  status: OrderStatus;
  paymentStatus: PaymentStatus;
  paymentMethod: PaymentMethod;
  fullName: string;
  mobile: string;
  total: number;
  createdAtUtc: string;
  hasTransferReference: boolean;
}

export interface AdminOrder {
  id: number;
  order: OrderDto;
  clientIp: string;
  statusLog: { from: OrderStatus | null; to: OrderStatus; adminUserId: string | null; atUtc: string }[];
}

export type ProductStatus = 'draft' | 'published' | 'archived';

export interface AdminCategory {
  id: number;
  slug: string;
  name: string;
  parentId: number | null;
  description: string | null;
  sortOrder: number;
  isActive: boolean;
}

export interface AdminBrand {
  id: number;
  slug: string;
  name: string;
  isActive: boolean;
}

export interface AdminAttribute {
  id: number;
  code: string;
  name: string;
  unit: string | null;
  type: 'option' | 'text';
  isFilterable: boolean;
  options: { id: number; value: string; slug: string; sortOrder: number }[];
}

export interface AdminVariant {
  id: number;
  sku: string;
  price: number;
  compareAtPrice: number | null;
  stock: number;
  isActive: boolean;
  sortOrder: number;
  rowVersion: string;
}

export interface AdminProduct {
  id: number;
  slug: string;
  name: string;
  description: string | null;
  categoryId: number;
  brandId: number | null;
  status: ProductStatus;
  createdAtUtc: string;
  updatedAtUtc: string;
  variants: AdminVariant[];
  images: { id: number; url: string; alt: string; sortOrder: number }[];
  attributes: { attributeId: number; variantId: number | null; optionId: number | null; text: string | null }[];
}

export interface AdminProductListItem {
  id: number;
  slug: string;
  name: string;
  status: ProductStatus;
  category: string;
  brand: string | null;
  variantCount: number;
  minPrice: number | null;
  totalStock: number;
  updatedAtUtc: string;
}

export interface StockHistoryEntry {
  id: number;
  atUtc: string;
  adminEmail: string | null;
  delta: number;
  stockAfter: number;
  reason: string | null;
  source: 'adjust' | 'edit';
}

export interface StoreSettings {
  shippingFlatFee: number;
  freeShippingThreshold: number | null;
  cardToCardEnabled: boolean;
  cashOnDeliveryEnabled: boolean;
  cardNumber: string;
  cardHolderName: string;
  bankName: string;
  maxQuantityPerLine: number;
  storePhone: string;
  storeAddress: string;
  workingHours: string;
  instagramHandle: string;
}

export interface AdminSettings {
  settings: StoreSettings;
  cardToCardOffered: boolean;
  updatedAtUtc: string;
  rowVersion: string;
}

export interface SettingsAuditEntry {
  id: number;
  atUtc: string;
  adminEmail: string | null;
  field: string;
  oldValue: string | null;
  newValue: string | null;
}
