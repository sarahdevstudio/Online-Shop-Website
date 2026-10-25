/**
 * Runs once when the production server starts: a missing or placeholder `ADMIN_SESSION_SECRET` stops the server instead of
 * letting the admin panel run with a guessable sealing key. Skipped while building (no secret is needed to build).
 */
export async function register() {
  if (process.env.NEXT_RUNTIME !== 'nodejs' || process.env.NODE_ENV !== 'production') return;
  if (process.env.NEXT_PHASE === 'phase-production-build') return;
  const { readSessionConfig } = await import('./lib/admin/session');
  readSessionConfig(); // throws with the reason
}
