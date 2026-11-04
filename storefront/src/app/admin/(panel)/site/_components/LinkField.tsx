'use client';

import { useRef, useState } from 'react';
import { ComboField, SelectField, TextField } from '@/components/form';
import { customValue, entityValue, kindOf, LINK_KINDS, targetKey, targetStatus, withKind, type LinkKind, type LinkValue } from '@/lib/admin/link-model';
import { siteEditorMessages } from '@/lib/admin/site-messages';
import type { LinkTarget } from '@/lib/admin/site-types';
import { formatNumber } from '@/lib/format';
import { searchLinkTargets } from './link-actions';

const t = siteEditorMessages.link;

const KIND_LABELS: Record<LinkKind, () => string> = {
  custom: () => t.kinds.custom,
  category: () => t.kinds.category,
  product: () => t.kinds.product,
  contentPage: () => t.kinds.contentPage,
  blogPost: () => t.kinds.blogPost,
};

interface Props {
  /** Field name of the link in the payload (`headerMenu[0].link`): the server's error for this link appears under it. */
  name: string;
  label: string;
  value: LinkValue;
  onChange: (next: LinkValue) => void;
  /** Entities the stored links point to (`links` of the site document), for their names and the deleted-entity warning. */
  known?: Readonly<Record<string, LinkTarget>>;
  required?: boolean;
}

/**
 * One link of the site: a typed URL (fallback) or an entity picked with a searchable select. An entity link follows the
 * entity's path; when the entity was deleted the editor says so and the site hides the link.
 */
export function LinkField({ name, label, value, onChange, known, required }: Props) {
  const kind = kindOf(value);
  const [typed, setTyped] = useState<string | null>(value.link);
  // A kind chosen but not yet filled in ("product", nothing picked) has no entity, so it is kept here.
  const [chosenKind, setChosenKind] = useState<LinkKind>(kind);
  const [picked, setPicked] = useState<Record<string, LinkTarget>>({});
  const foundRef = useRef<Record<string, LinkTarget>>({}); // the last search results, so a pick keeps the target's path and state
  const shownKind = value.entity ? kind : chosenKind;
  const status = targetStatus(value.entity, { ...known, ...picked });

  function changeKind(next: LinkKind) {
    setChosenKind(next);
    if (kind === 'custom') setTyped(value.link);
    onChange(withKind(value, next, typed));
  }

  return (
    <>
      <SelectField name={`${name}.kind`} label={`${label} — ${t.kind}`} value={shownKind} onChange={(e) => changeKind(e.target.value as LinkKind)}>
        {LINK_KINDS.map((k) => (
          <option key={k} value={k}>
            {KIND_LABELS[k]()}
          </option>
        ))}
      </SelectField>
      {shownKind === 'custom' ? (
        <TextField name={name} label={label} value={value.link ?? ''} onChange={(e) => onChange(customValue(e.target.value))} ltr autoComplete="off" required={required} />
      ) : (
        <ComboField
          name={name}
          label={`${label} — ${t.target}`}
          hint={status && !status.deleted ? (status.path ? t.currentPath(status.path) : t.targetHint) : t.targetHint}
          required={required}
          selectedLabel={status && !status.deleted ? status.label : ''}
          placeholder={value.entity ? undefined : t.notChosen}
          messages={{ searching: t.searching, noResults: t.noResults, results: t.results }}
          formatCount={(n) => formatNumber(n)}
          search={async (query) => {
            const found = await searchLinkTargets(shownKind, query);
            for (const target of found) foundRef.current[targetKey(target)] = target;
            return found.map((target) => ({
              value: targetKey(target),
              label: target.label,
              detail: [target.path, target.state in t.stateBadges ? t.stateBadges[target.state as keyof typeof t.stateBadges] : null].filter(Boolean).join(' · ') || undefined,
            }));
          }}
          onSelect={(option) => {
            const [type, id] = option.value.split(':');
            const found = foundRef.current[option.value];
            const target: LinkTarget = found ?? { type: type as LinkTarget['type'], id: Number(id), label: option.label, path: null, exists: true, state: 'published' };
            setPicked((p) => ({ ...p, [option.value]: target }));
            onChange(entityValue(target));
          }}
        />
      )}
      {status?.deleted && (
        <p className="ui-error" role="status">
          {status.label ? t.deleted(status.label) : t.deletedUnknown}
        </p>
      )}
      {status && !status.deleted && (status.state === 'draft' || status.state === 'inactive' || status.state === 'archived') && (
        <p className="ui-error" role="status">
          {t.hidden(status.label, status.state === 'draft' ? t.states.draft : status.state === 'inactive' ? t.states.inactive : t.states.archived)}
        </p>
      )}
    </>
  );
}
