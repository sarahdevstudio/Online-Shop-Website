import { adminGet } from '../admin/api';
import { clientTexts, ensureAdminTexts } from './texts-server';

/** Loads the admin panel's texts with the admin's own session (the bundle endpoint needs one); a failure keeps the shipped texts. */
export async function loadAdminTexts() {
  await ensureAdminTexts(async () => {
    const bundle = await adminGet<Record<string, string>>('/admin/translations/bundle?prefix=admin.');
    if (!bundle) throw new Error('no admin texts');
    return bundle;
  });
  return clientTexts(['admin.', 'common.']);
}

/** After an edit: this instance reloads both dictionaries at once (other instances within the cache time). */
export async function refreshAllTexts() {
  const { ensureTexts } = await import('./texts-server');
  await Promise.allSettled([
    ensureTexts(true),
    ensureAdminTexts(async () => {
      const bundle = await adminGet<Record<string, string>>('/admin/translations/bundle?prefix=admin.');
      if (!bundle) throw new Error('no admin texts');
      return bundle;
    }, true),
  ]);
}
