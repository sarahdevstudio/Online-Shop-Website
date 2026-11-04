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
  adminUsername: string | null;
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
  adminUsername: string | null;
  field: string;
  oldValue: string | null;
  newValue: string | null;
}

export interface AdminContentListItem {
  slug: string;
  title: string;
  isDraft: boolean;
  updatedAtUtc: string;
  updatedByUsername: string | null;
}

export interface AdminContentPage {
  id: number;
  slug: string;
  title: string;
  body: string;
  isDraft: boolean;
  updatedAtUtc: string;
  updatedByUsername: string | null;
  rowVersion: string;
}

export interface ContentAuditEntry {
  atUtc: string;
  adminUsername: string | null;
  titleChanged: boolean;
  bodyChanged: boolean;
}

export interface AdminBlogListItem {
  id: number;
  slug: string;
  title: string;
  isPublished: boolean;
  publishedAtUtc: string | null;
  updatedAtUtc: string;
  updatedByUsername: string | null;
}

export interface AdminBlogPost extends AdminBlogListItem {
  excerpt: string;
  body: string;
  rowVersion: string;
}

export interface BlogAuditEntry {
  atUtc: string;
  action: string;
  adminUsername: string | null;
}

export interface AdminTranslation {
  id: number;
  key: string;
  culture: string;
  text: string;
  defaultText: string;
  isEdited: boolean;
  inUse: boolean;
  placeholders: string[];
  updatedAtUtc: string;
  updatedBy: string | null;
  rowVersion: string;
}

export interface TranslationArea {
  area: string;
  count: number;
  edited: number;
}

export interface TranslationAuditEntry {
  atUtc: string;
  adminUsername: string | null;
  oldText: string;
  newText: string;
}

/** An entity's current public path (with the version the form sends back) and the old paths that redirect to it. */
export interface RouteTarget {
  type: 'category' | 'product' | 'contentPage' | 'blogPost';
  id: number;
  path: string;
  rowVersion: string;
  oldPaths: { id: number; path: string; createdAtUtc: string }[];
}

/** One row of the redirects page: an old path (or the path of a deleted entity) and where it leads. */
export interface RedirectRow {
  id: number;
  path: string;
  targetType: string;
  targetId: number;
  targetName: string;
  currentPath: string | null;
  isGone: boolean;
  createdAtUtc: string;
}

export interface RouteAuditEntry {
  atUtc: string;
  targetType: string;
  targetId: number;
  targetName: string;
  action: string;
  oldPath: string | null;
  newPath: string | null;
  adminUsername: string | null;
}
