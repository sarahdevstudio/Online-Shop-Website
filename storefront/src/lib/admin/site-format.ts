/** The API's timestamps are UTC without a trailing `Z`. Shown in the Persian calendar, Tehran time. */
export function formatSiteTime(value: string | null | undefined): string {
  if (!value) return '—';
  const iso = /[zZ]|[+-]\d\d:\d\d$/.test(value) ? value : `${value}Z`;
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '—';
  return new Intl.DateTimeFormat('fa-IR-u-ca-persian', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    timeZone: 'Asia/Tehran',
  }).format(date);
}
