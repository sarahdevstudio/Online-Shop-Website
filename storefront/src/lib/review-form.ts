/** State of the guest review form (kept out of the 'use server' file: such a file may export only actions). */
export interface ReviewFormState {
  ok: boolean;
  message: string | null;
  errors: Record<string, string>;
  /** What was typed, so a refused submit can show it again (never the honeypot). */
  values: { name: string; mobile: string; rating: string; text: string };
}

export const INITIAL_REVIEW_STATE: ReviewFormState = { ok: false, message: null, errors: {}, values: { name: '', mobile: '', rating: '', text: '' } };
