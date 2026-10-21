/**
 * Icon set: 24x24 outline paths (stroke 1.75, round caps), hand-picked for the shop.
 * Paths are plain data so icons render as real SVG elements with no HTML sanitizer bypass.
 *
 * "chevron-start" / "chevron-end" point toward the start/end of the reading direction and are mirrored
 * automatically in RTL; use them for previous/next. Physical-direction icons are deliberately absent.
 */
export const ICONS = {
  search: ['M18 11a7 7 0 1 1-14 0 7 7 0 0 1 14 0Z', 'm21 21-4.3-4.3'],
  cart: [
    'M3 4h2l2.4 11.2a2 2 0 0 0 2 1.6h7.7a2 2 0 0 0 2-1.5L21 8H6.2',
    'M10 20.5a.5.5 0 1 1-1 0 .5.5 0 0 1 1 0Z',
    'M18 20.5a.5.5 0 1 1-1 0 .5.5 0 0 1 1 0Z',
  ],
  user: ['M19 21v-1.5a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4V21', 'M16 7.5a4 4 0 1 1-8 0 4 4 0 0 1 8 0Z'],
  menu: ['M4 6h16', 'M4 12h16', 'M4 18h16'],
  x: ['M18 6 6 18', 'm6 6 12 12'],
  heart: [
    'M12 20.5s-7.5-4.6-9.2-9.3C1.6 7.9 3.6 4.5 7 4.5c2 0 3.5 1 5 3 1.5-2 3-3 5-3 3.4 0 5.4 3.4 4.2 6.7-1.7 4.7-9.2 9.3-9.2 9.3Z',
  ],
  star: ['m12 3 2.7 5.6 6.1.9-4.4 4.3 1 6.1L12 17l-5.4 2.9 1-6.1L3.2 9.5l6.1-.9L12 3Z'],
  'chevron-down': ['m6 9 6 6 6-6'],
  'chevron-up': ['m18 15-6-6-6 6'],
  'chevron-start': ['m15 6-6 6 6 6'],
  'chevron-end': ['m9 6 6 6-6 6'],
  check: ['M20 6 9 17l-5-5'],
  alert: [
    'M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0Z',
    'M12 9v4',
    'M12 17h.01',
  ],
  info: ['M22 12a10 10 0 1 1-20 0 10 10 0 0 1 20 0Z', 'M12 16v-4', 'M12 8h.01'],
  trash: ['M3 6h18', 'M8 6V4a1 1 0 0 1 1-1h6a1 1 0 0 1 1 1v2', 'M6 6l1 14a2 2 0 0 0 2 2h6a2 2 0 0 0 2-2l1-14'],
  plus: ['M12 5v14', 'M5 12h14'],
  minus: ['M5 12h14'],
  filter: ['M3 5h18l-7 8.5V20l-4-2v-4.5L3 5Z'],
  home: ['m3 11 9-8 9 8', 'M5 10v10h5v-6h4v6h5V10'],
  phone: ['M5 4h3.5l1.8 4.5-2.2 1.4a11 11 0 0 0 5.5 5.5l1.4-2.2L20 15.5V19a1 1 0 0 1-1 1A15 15 0 0 1 4 5a1 1 0 0 1 1-1Z'],
  mail: ['M4 6h16a1 1 0 0 1 1 1v10a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1Z', 'm3 7 9 6 9-6'],
  'map-pin': ['M12 21s-7-6.2-7-11.5a7 7 0 0 1 14 0C19 14.8 12 21 12 21Z', 'M15 9.5a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z'],
  truck: [
    'M2 6h11v10H2Z',
    'M13 9h4l3 3v4h-7',
    'M7.5 18.5a1.5 1.5 0 1 1-3 0 1.5 1.5 0 0 1 3 0Z',
    'M18.5 18.5a1.5 1.5 0 1 1-3 0 1.5 1.5 0 0 1 3 0Z',
  ],
  'shield-check': ['M12 3 4 6v6c0 4.5 3.2 8 8 9 4.8-1 8-4.5 8-9V6l-8-3Z', 'm9 12 2 2 4-4'],
  refresh: ['M20 11a8 8 0 0 0-14.5-4', 'M4 4v4h4', 'M4 13a8 8 0 0 0 14.5 4', 'M20 20v-4h-4'],
  package: ['M12 3 3 7.5v9L12 21l9-4.5v-9L12 3Z', 'm3 7.5 9 4.5 9-4.5', 'M12 12v9'],
  image: [
    'M5 4h14a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2Z',
    'm3 16 5-5 4 4 3-3 6 6',
    'M9 9.5a1.5 1.5 0 1 1-3 0 1.5 1.5 0 0 1 3 0Z',
  ],
  moon: ['M20 14.5A8 8 0 0 1 9.5 4 8 8 0 1 0 20 14.5Z'],
  sun: [
    'M16 12a4 4 0 1 1-8 0 4 4 0 0 1 8 0Z',
    'M12 2v2',
    'M12 20v2',
    'm4.9 4.9 1.4 1.4',
    'm17.7 17.7 1.4 1.4',
    'M2 12h2',
    'M20 12h2',
    'm4.9 19.1 1.4-1.4',
    'm17.7 6.3 1.4-1.4',
  ],
} as const satisfies Record<string, readonly string[]>;

export type IconName = keyof typeof ICONS;

/** Icons that must be mirrored when the document direction is RTL. */
export const DIRECTIONAL_ICONS: ReadonlySet<IconName> = new Set<IconName>(['chevron-start', 'chevron-end']);
