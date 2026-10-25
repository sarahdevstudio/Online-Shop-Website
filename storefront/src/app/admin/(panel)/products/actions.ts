'use server';

import { revalidatePath } from 'next/cache';
import { redirect } from 'next/navigation';
import { adminFetch } from '@/lib/admin/api';
import { failState, okState, problemState, submittedFrom, type FormState } from '@/lib/admin/form-state';
import { checked, id as readId, text, whole } from '@/lib/admin/form-input';
import { adminMessages } from '@/lib/admin/messages';
import type { AdminAttribute, AdminProduct } from '@/lib/admin/types';

const STATUSES = ['draft', 'published', 'archived'];

interface VariantBody {
  sku: string;
  price: number;
  compareAtPrice: number | null;
  stock: number;
  isActive: boolean;
  sortOrder: number;
}

function variantFrom(formData: FormData, prefix = ''): VariantBody {
  return {
    sku: text(formData, `${prefix}sku`),
    price: whole(formData, `${prefix}price`) ?? 0,
    compareAtPrice: whole(formData, `${prefix}compareAtPrice`),
    stock: whole(formData, `${prefix}stock`) ?? 0,
    isActive: prefix === '' ? checked(formData, 'isActive') : true,
    sortOrder: whole(formData, `${prefix}sortOrder`) ?? 0,
  };
}

function productBody(formData: FormData) {
  const status = text(formData, 'status');
  return {
    categoryId: whole(formData, 'categoryId') ?? 0,
    brandId: whole(formData, 'brandId'),
    name: text(formData, 'name'),
    description: text(formData, 'description') || null,
    status: STATUSES.includes(status) ? status : 'draft',
  };
}

export async function createProductAction(_previous: FormState, formData: FormData): Promise<FormState> {
  const submitted = submittedFrom(formData);
  const variants: VariantBody[] = [];
  for (let i = 0; formData.has(`variants.${i}.sku`); i++) {
    variants.push({ ...variantFrom(formData, `variants.${i}.`), isActive: true, sortOrder: i });
  }
  const result = await adminFetch<AdminProduct>('POST', '/admin/products', { body: { ...productBody(formData), slug: text(formData, 'slug'), variants } });
  if (!result.ok) return problemState(result.problem, submitted);
  redirect(`/admin/products/${result.body.id}`);
}

export async function updateProductAction(_previous: FormState, formData: FormData): Promise<FormState> {
  const productId = readId(formData);
  if (productId === null) return failState(adminMessages.failed);
  const result = await adminFetch<AdminProduct>('PUT', `/admin/products/${productId}`, { body: { ...productBody(formData), slug: null, variants: null } });
  if (!result.ok) return problemState(result.problem, submittedFrom(formData));
  revalidatePath(`/admin/products/${productId}`);
  return okState(adminMessages.saved);
}

/** Exact values (SKU, prices, active flag, "set stock to N"); carries the row version, so a concurrent change is a 409. */
export async function saveVariantAction(_previous: FormState, formData: FormData): Promise<FormState> {
  const variantId = readId(formData, 'variantId');
  const productId = readId(formData, 'productId');
  if (variantId === null || productId === null) return failState(adminMessages.failed);
  const body = { ...variantFrom(formData), rowVersion: text(formData, 'rowVersion') };
  const result = await adminFetch<unknown>('PUT', `/admin/variants/${variantId}`, { body });
  if (!result.ok) return problemState(result.problem, submittedFrom(formData));
  revalidatePath(`/admin/products/${productId}`);
  return okState(adminMessages.saved);
}

export async function addVariantAction(_previous: FormState, formData: FormData): Promise<FormState> {
  const productId = readId(formData, 'productId');
  if (productId === null) return failState(adminMessages.failed);
  const result = await adminFetch<unknown>('POST', `/admin/products/${productId}/variants`, { body: variantFrom(formData) });
  if (!result.ok) return problemState(result.problem, submittedFrom(formData));
  revalidatePath(`/admin/products/${productId}`);
  return okState(adminMessages.saved);
}

/** Atomic +/- on the stock; no row version, so it never fails because a customer bought something a second ago. */
export async function adjustStockAction(_previous: FormState, formData: FormData): Promise<FormState> {
  const variantId = readId(formData, 'variantId');
  const productId = readId(formData, 'productId');
  const delta = whole(formData, 'delta');
  if (variantId === null || productId === null || delta === null || delta === 0) {
    return failState(adminMessages.fixErrors, {}, { delta: adminMessages.products.adjustHint });
  }
  const result = await adminFetch<unknown>('POST', `/admin/variants/${variantId}/stock-adjust`, { body: { delta, reason: text(formData, 'reason') || null } });
  if (!result.ok) return problemState(result.problem, submittedFrom(formData));
  revalidatePath(`/admin/products/${productId}`);
  return okState(adminMessages.saved);
}

/** Replaces the product's whole attribute value set from fields named `attr:<attributeId>:<p | variantId>`. */
export async function saveAttributesAction(_previous: FormState, formData: FormData): Promise<FormState> {
  const productId = readId(formData, 'productId');
  if (productId === null) return failState(adminMessages.failed);

  const attributes = await adminFetch<AdminAttribute[]>('GET', '/admin/attributes');
  if (!attributes.ok) return problemState(attributes.problem);
  const byId = new Map(attributes.body.map((a) => [a.id, a]));

  const values: { attributeId: number; variantId: number | null; optionId: number | null; text: string | null }[] = [];
  for (const [key, raw] of formData.entries()) {
    const match = /^attr:(\d+):(p|\d+)$/.exec(key);
    if (!match || typeof raw !== 'string' || raw.trim() === '') continue;
    const attribute = byId.get(Number(match[1]));
    if (!attribute) continue;
    const variantId = match[2] === 'p' ? null : Number(match[2]);
    if (attribute.type === 'option') {
      const optionId = Number(raw);
      if (Number.isSafeInteger(optionId)) values.push({ attributeId: attribute.id, variantId, optionId, text: null });
    } else {
      values.push({ attributeId: attribute.id, variantId, optionId: null, text: raw.trim() });
    }
  }

  const result = await adminFetch<unknown>('PUT', `/admin/products/${productId}/attributes`, { body: { values } });
  if (!result.ok) return problemState(result.problem);
  revalidatePath(`/admin/products/${productId}`);
  return okState(adminMessages.saved);
}

export async function reorderImagesAction(_previous: FormState, formData: FormData): Promise<FormState> {
  const productId = readId(formData, 'productId');
  const imageIds = text(formData, 'order')
    .split(',')
    .map(Number)
    .filter((n) => Number.isSafeInteger(n) && n > 0);
  if (productId === null || imageIds.length === 0) return failState(adminMessages.failed);
  const result = await adminFetch<unknown>('PUT', `/admin/products/${productId}/images/order`, { body: { imageIds } });
  if (!result.ok) return problemState(result.problem);
  revalidatePath(`/admin/products/${productId}`);
  return okState(adminMessages.saved);
}

export async function deleteImageAction(_previous: FormState, formData: FormData): Promise<FormState> {
  const productId = readId(formData, 'productId');
  const imageId = readId(formData, 'imageId');
  if (productId === null || imageId === null) return failState(adminMessages.failed);
  const result = await adminFetch<unknown>('DELETE', `/admin/images/${imageId}`);
  if (!result.ok) return problemState(result.problem);
  revalidatePath(`/admin/products/${productId}`);
  return okState(adminMessages.taxonomy.deleted);
}
