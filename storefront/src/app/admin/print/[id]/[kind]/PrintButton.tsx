'use client';

import { printMessages } from '@/lib/admin/print-messages';

/** Opens the browser's print dialog for the sheet (the controls are hidden by the print stylesheet). */
export function PrintButton() {
  return (
    <button type="button" onClick={() => window.print()}>
      {printMessages.print}
    </button>
  );
}
