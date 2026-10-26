import { messages } from './messages';
import { persianOr } from './persian-only';

export const CHECKOUT_TEXT_FIELDS = ['fullName', 'mobile', 'province', 'city', 'address', 'postalCode', 'note'] as const;

export type CheckoutField = (typeof CHECKOUT_TEXT_FIELDS)[number];
export type CheckoutValues = Record<CheckoutField, string> & { paymentMethod: string };

export interface CheckoutState {
  values: CheckoutValues;
  /** field → first message; shown next to the field. */
  errors: Record<string, string>;
  formError: string | null;
}

export const EMPTY_CHECKOUT_VALUES: CheckoutValues = {
  fullName: '',
  mobile: '',
  province: '',
  city: '',
  address: '',
  postalCode: '',
  note: '',
  paymentMethod: '',
};

export const INITIAL_CHECKOUT_STATE: CheckoutState = { values: EMPTY_CHECKOUT_VALUES, errors: {}, formError: null };

/** What the customer typed, as strings, so a failed submit can show it again. */
export function readCheckoutValues(formData: FormData): CheckoutValues {
  const read = (name: string) => {
    const value = formData.get(name);
    return typeof value === 'string' ? value : '';
  };
  return {
    fullName: read('fullName'),
    mobile: read('mobile'),
    province: read('province'),
    city: read('city'),
    address: read('address'),
    postalCode: read('postalCode'),
    note: read('note'),
    paymentMethod: read('paymentMethod'),
  };
}

/** API validation keys are PascalCase property names ("FullName"); the form uses camelCase. First message per field. */
export function mapApiErrors(errors: Record<string, string[]> | null): Record<string, string> {
  const result: Record<string, string> = {};
  for (const [key, list] of Object.entries(errors ?? {})) {
    if (list.length > 0) result[key.charAt(0).toLowerCase() + key.slice(1)] = persianOr(list[0], messages.invalidValue);
  }
  return result;
}

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function isIdempotencyKey(value: FormDataEntryValue | null): value is string {
  return typeof value === 'string' && UUID.test(value);
}
