import type { AdminProblem } from './api';
import { failState, type FormState } from './form-state';
import { adminMessages } from './messages';

/** The admin forms' side of dynamic routing (docs/11 C.3): normalizing what is typed, previewing the URL, mapping API refusals. */

/** Mirrors the API's `RoutePath.Normalize`: trimmed, ASCII letters lower-cased, one leading "/", no trailing "/". "" stays "". */
export function normalizePathInput(raw: string): string {
  const trimmed = raw.trim().replace(/[A-Z]/g, (c) => c.toLowerCase()).replace(/\/+$/, '');
  if (trimmed === '') return '';
  return trimmed.startsWith('/') ? trimmed : `/${trimmed}`;
}

/** What the visitor will see in the address bar for what is typed (the default path when nothing is typed). */
export function resultingUrl(origin: string, typed: string, fallbackPath: string): string {
  const path = normalizePathInput(typed) || fallbackPath;
  return `${origin.replace(/\/+$/, '')}${path}`;
}

/** The path the form field starts with: empty on create (the API then uses the default path), the current path on edit. */
export const defaultPathFor = (type: 'category' | 'product' | 'contentPage' | 'blogPost', slug: string): string => {
  switch (type) {
    case 'category':
      return `/category/${slug}`;
    case 'product':
      return `/product/${slug}`;
    case 'blogPost':
      return `/blog/${slug}`;
    default:
      return `/${slug}`;
  }
};

/**
 * The API's refusal of a path as form state: the Persian message (translated by the API, editable in the translations editor)
 * goes next to the path field; a stale version is the usual "data changed" conflict. Null when the problem is not about the path.
 */
export function pathProblemState(problem: AdminProblem, submitted: Record<string, string>): FormState | null {
  const field = problem.errors && Object.entries(problem.errors).find(([key]) => key.toLowerCase() === 'path')?.[1]?.[0];
  if (problem.status === 400 && field) return failState(adminMessages.fixErrors, submitted, { path: field });
  if (problem.status === 409 && problem.code?.startsWith('path_')) {
    return failState(adminMessages.fixErrors, submitted, { path: problem.detail || adminMessages.invalidValue });
  }
  if (problem.status === 409) return { ...failState(adminMessages.conflict, submitted), conflict: true };
  return null;
}
