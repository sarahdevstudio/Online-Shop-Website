import MarkdownIt from 'markdown-it';

/**
 * Markdown for the info pages, rendered safely. The text is written by an admin, but it is rendered into every visitor's
 * page, so it is treated as untrusted:
 *  - raw HTML is NOT enabled (`html: false`): `<script>`, `<img onerror=...>` and the like come out as escaped text;
 *  - images are disabled (no `data:` or remote image tricks);
 *  - a link survives only with an http(s), mailto, tel or relative target, whatever the case, entities or control
 *    characters used to hide the scheme; otherwise the text is shown without a link;
 *  - every link gets `rel="noopener"` (external ones also `noreferrer nofollow` and open in a new tab).
 * markdown-it escapes every attribute value it writes, so a link title can't inject an event handler.
 */
const md = new MarkdownIt({ html: false, linkify: false, typographer: false, breaks: false });
md.disable(['image']);

// Written with explicit escapes inside a string, so no invisible character sits in the source.
const IGNORED_IN_SCHEME = new RegExp('[\\u0000-\\u0020\\u007f-\\u009f\\u200b-\\u200f\\u2028\\u2029\\ufeff]', 'g');

const ALLOWED_SCHEMES = new Set(['http', 'https', 'mailto', 'tel']);

/** True for targets that are safe to link to: allowed scheme, or a path / anchor with no scheme (not protocol-relative). */
export function isSafeLink(url: string): boolean {
  // Remove what browsers ignore inside a scheme (tabs, newlines, other control characters, spaces) before looking at it.
  const compact = url.replace(IGNORED_IN_SCHEME, '');
  if (compact === '') return false;
  const scheme = /^([a-z][a-z0-9+.-]*):/i.exec(compact);
  if (scheme) return ALLOWED_SCHEMES.has(scheme[1].toLowerCase());
  return !/^(?:\/\/|\/\\|\\)/.test(compact);
}

md.validateLink = isSafeLink;

const renderLinkOpen = md.renderer.rules.link_open ?? ((tokens, idx, options, _env, self) => self.renderToken(tokens, idx, options));
md.renderer.rules.link_open = (tokens, idx, options, env, self) => {
  const token = tokens[idx];
  const external = /^https?:/i.test((token.attrGet('href') ?? '').trim());
  token.attrSet('rel', external ? 'noopener noreferrer nofollow' : 'noopener');
  if (external) token.attrSet('target', '_blank');
  return renderLinkOpen(tokens, idx, options, env, self);
};

/** Markdown → HTML that is safe to put in the page (see the module comment). */
export function renderMarkdown(source: string): string {
  return md.render(source);
}

/** A short plain-text summary for `<meta name="description">`: the first real paragraph, without markup or the draft banner. */
export function plainDescription(source: string, max = 160): string {
  const paragraphs = source
    .split(/\n\s*\n/)
    .map((block) => block.trim())
    .filter((block) => block !== '' && !block.startsWith('>') && !block.startsWith('#'));
  const text = (paragraphs[0] ?? '')
    .replace(/\[([^\]]*)\]\([^)]*\)/g, '$1')
    .replace(/[*_`>#-]/g, '')
    .replace(/\s+/g, ' ')
    .trim();
  return text.length <= max ? text : `${text.slice(0, max - 1).trimEnd()}…`;
}
