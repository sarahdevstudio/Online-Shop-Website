import { DIRECTIONAL_ICONS, ICONS, type IconName } from '@/lib/icons';

interface IconProps {
  name: IconName;
  label?: string;
  filled?: boolean;
  className?: string;
}

/** Inline SVG icon; decorative unless `label` is given. Size follows --icon-size. */
export function Icon({ name, label, filled = false, className }: IconProps) {
  const classes = ['icon', DIRECTIONAL_ICONS.has(name) ? 'ui-icon--directional' : '', className ?? ''].filter(Boolean).join(' ');
  return (
    <span className={classes} style={{ display: 'inline-flex', flex: 'none', inlineSize: 'var(--icon-size, 1.25rem)', blockSize: 'var(--icon-size, 1.25rem)' }}>
      <svg
        viewBox="0 0 24 24"
        width="100%"
        height="100%"
        fill={filled ? 'currentColor' : 'none'}
        stroke="currentColor"
        strokeWidth={1.75}
        strokeLinecap="round"
        strokeLinejoin="round"
        role={label ? 'img' : undefined}
        aria-label={label}
        aria-hidden={label ? undefined : true}
        focusable="false"
      >
        {ICONS[name].map((d) => (
          <path key={d} d={d} />
        ))}
      </svg>
    </span>
  );
}
