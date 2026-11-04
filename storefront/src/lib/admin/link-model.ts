import { LINK_ENTITY_TYPES, type LinkEntity, type LinkEntityType, type LinkTarget } from './site-types';

/**
 * The editor-side model of a site link: either a typed URL ("custom", the fallback) or a reference to an entity of the shop
 * (category, product, info page, blog post). A reference is stored as (type, id) and the site shows the entity's current path.
 */

export type LinkKind = 'custom' | LinkEntityType;

export interface LinkValue {
  link: string | null;
  entity: LinkEntity | null;
}

export const LINK_KINDS: readonly LinkKind[] = ['custom', ...LINK_ENTITY_TYPES];

export const isLinkEntityType = (value: string): value is LinkEntityType => (LINK_ENTITY_TYPES as readonly string[]).includes(value);

export const targetKey = (entity: Pick<LinkEntity, 'type' | 'id'>) => `${entity.type}:${entity.id}`;

export const kindOf = (value: LinkValue): LinkKind => value.entity?.type ?? 'custom';

/** The value to store after choosing an entity: the typed URL is dropped (the entity wins on the server too). */
export const entityValue = (target: Pick<LinkTarget, 'type' | 'id'>): LinkValue => ({ link: null, entity: { type: target.type, id: target.id } });

export const customValue = (link: string | null): LinkValue => ({ link, entity: null });

/** Switching the kind keeps what makes sense: the typed URL survives a round trip through "entity", the chosen entity does not. */
export function withKind(value: LinkValue, kind: LinkKind, typedBefore: string | null): LinkValue {
  if (kind === 'custom') return customValue(value.link ?? typedBefore);
  return { link: null, entity: value.entity?.type === kind ? value.entity : null };
}

/** A required link (menu child, footer link) is `""` rather than `null` while it points to an entity. */
export const requiredLink = (value: LinkValue): { link: string; entity: LinkEntity | null } => ({ link: value.entity ? '' : (value.link ?? ''), entity: value.entity });

/** An optional link (item, announcement, slide, banner): `null` when empty. */
export function optionalLink(value: LinkValue): { link: string | null; entity: LinkEntity | null } {
  if (value.entity) return { link: null, entity: value.entity };
  const link = value.link?.trim();
  return { link: link ? link : null, entity: null };
}

/** The hint under a stored entity link: where it leads now, or why it does not show. */
export function targetStatus(entity: LinkEntity | null, known: Readonly<Record<string, LinkTarget>> | undefined): { label: string; path: string | null; deleted: boolean } | null {
  if (!entity) return null;
  const target = known?.[targetKey(entity)];
  if (!target) return { label: '', path: null, deleted: false };
  return { label: target.label, path: target.path, deleted: !target.exists };
}
