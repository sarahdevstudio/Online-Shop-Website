import { contrastRatio, onColor, persianRatio, type ContrastItem, type ThemeColors } from '../contrast';
import { siteMessages } from './site-messages';

/** The WCAG AA report for the palette editor (live) - same pairs and thresholds as the API's Contrast.Check. */
interface PairSpec {
  id: string;
  foreground: (theme: ThemeColors) => string;
  background: (theme: ThemeColors) => string;
  min: number;
}

const PAIRS: readonly PairSpec[] = [
  { id: 'text-background', foreground: (t) => t.text, background: (t) => t.background, min: 4.5 },
  { id: 'text-surface', foreground: (t) => t.text, background: (t) => t.surface, min: 4.5 },
  { id: 'primary-background', foreground: (t) => t.primary, background: (t) => t.background, min: 4.5 },
  { id: 'primary-surface', foreground: (t) => t.primary, background: (t) => t.surface, min: 4.5 },
  { id: 'on-primary', foreground: (t) => onColor(t.primary), background: (t) => t.primary, min: 4.5 },
  { id: 'on-secondary', foreground: (t) => onColor(t.secondary), background: (t) => t.secondary, min: 4.5 },
  { id: 'on-accent', foreground: (t) => onColor(t.accent), background: (t) => t.accent, min: 4.5 },
  { id: 'success-background', foreground: (t) => t.success, background: (t) => t.background, min: 4.5 },
  { id: 'warning-background', foreground: (t) => t.warning, background: (t) => t.background, min: 4.5 },
  { id: 'danger-background', foreground: (t) => t.danger, background: (t) => t.background, min: 4.5 },
  { id: 'focus-ring', foreground: (t) => t.primary, background: (t) => t.background, min: 3 },
];

/** The WCAG AA report for a palette whose colors are all valid `#rrggbb`. */
export function contrastReport(theme: ThemeColors): ContrastItem[] {
  return PAIRS.map((pair) => {
    const foreground = pair.foreground(theme);
    const background = pair.background(theme);
    const ratio = contrastRatio(foreground, background);
    return {
      id: pair.id,
      foreground,
      background,
      ratio: Math.floor(ratio * 100) / 100,
      min: pair.min,
      ok: ratio >= pair.min,
      message: siteMessages.theme.contrastMessage(siteMessages.theme.contrastTitles[pair.id], persianRatio(ratio), persianRatio(pair.min)),
    };
  });
}
