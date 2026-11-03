'use client';

import type { ReactNode } from 'react';
import { currentTexts, setTexts } from './texts';

/**
 * Hands the edited texts to client components. It renders before them, in the browser, so a client component that reads a text
 * during its first render sees the same words the server wrote into the HTML (on the server the dictionary is already loaded in
 * the process, so the provider must not touch it). Only the texts that differ from the shipped ones are passed (usually none).
 */
export function TextsProvider({ texts, children }: { texts: Record<string, string>; children: ReactNode }) {
  if (typeof window !== 'undefined') setTexts({ ...currentTexts(), ...texts });
  return <>{children}</>;
}
