import { describe, expect, it } from 'vitest';
import { isSafeLink, plainDescription, renderMarkdown } from '../src/lib/markdown';

/** True when the output contains a real element with an inline event handler (`<tag ... onxxx=`). */
const attributeNames = (html: string) =>
  [...html.matchAll(/<[a-z][^>]*>/gi)].flatMap((tag) => [...tag[0].matchAll(/\s([a-z-]+)\s*=\s*"[^"]*"/gi)].map((m) => m[1].toLowerCase()));
const hasEventHandlerAttribute = (html: string) => attributeNames(html).some((name) => name.startsWith('on'));
const hasScriptElement = (html: string) => /<script[\s>]/i.test(html);
const anchors = (html: string) => [...html.matchAll(/<a\s[^>]*>/gi)].map((m) => m[0]);

describe('renderMarkdown: ordinary content', () => {
  it('renders headings, lists, emphasis and Persian text', () => {
    const html = renderMarkdown('## مرجوعی\n\n- **۷ روز**\n- *بسته‌بندی اصلی*\n\nمتن ساده.');
    expect(html).toContain('<h2>مرجوعی</h2>');
    expect(html).toContain('<strong>۷ روز</strong>');
    expect(html).toContain('<em>بسته‌بندی اصلی</em>');
    expect(html).toContain('<li>');
    expect(html).toContain('<p>متن ساده.</p>');
  });

  it('renders the draft banner blockquote', () => {
    expect(renderMarkdown('> **[پیش‌نویس — بازبینی شود]** متن')).toContain('<blockquote>');
  });
});

describe('renderMarkdown: raw HTML is never rendered', () => {
  it.each([
    ['script tag', '<script>alert(1)</script>'],
    ['script with src', '<script src="https://evil.example/x.js"></script>'],
    ['img onerror', '<img src=x onerror=alert(1)>'],
    ['svg onload', '<svg onload=alert(1)>'],
    ['div onclick block', '<div onclick="alert(1)">click</div>'],
    ['inline html in a sentence', 'hello <b onmouseover="alert(1)">bold</b> world'],
    ['iframe', '<iframe src="javascript:alert(1)"></iframe>'],
    ['style tag', '<style>body{display:none}</style>'],
    ['anchor with js href', '<a href="javascript:alert(1)">x</a>'],
    ['mixed case', '<ScRiPt>alert(1)</sCrIpT>'],
    ['comment trick', '<!--><script>alert(1)</script>-->'],
    ['html entity encoded tag', '&lt;script&gt;alert(1)&lt;/script&gt;'],
  ])('%s becomes inert text', (_name, payload) => {
    const html = renderMarkdown(payload);
    expect(hasScriptElement(html)).toBe(false);
    expect(hasEventHandlerAttribute(html)).toBe(false);
    expect(html).not.toMatch(/<(img|svg|iframe|style|div|b)[\s>]/i);
    expect(html).not.toMatch(/<a\s+href="javascript:/i);
  });

  it('shows the tags as escaped text, so nothing is lost silently', () => {
    expect(renderMarkdown('<script>alert(1)</script>')).toContain('&lt;script&gt;');
  });
});

describe('renderMarkdown: links', () => {
  it.each([
    ['plain javascript:', '[x](javascript:alert(1))'],
    ['mixed case', '[x](JaVaScRiPt:alert(1))'],
    ['entity-encoded scheme', '[x](java&#115;cript:alert(1))'],
    ['numeric entity first letter', '[x](&#106;avascript:alert(1))'],
    ['tab inside the scheme', '[x](java\tscript:alert(1))'],
    ['leading whitespace', '[x]( javascript:alert(1))'],
    ['data html', '[x](data:text/html;base64,PHNjcmlwdD5hbGVydCgxKTwvc2NyaXB0Pg==)'],
    ['vbscript', '[x](vbscript:msgbox(1))'],
    ['file scheme', '[x](file:///etc/passwd)'],
    ['angle-bracket autolink', '<javascript:alert(1)>'],
    ['reference style', '[x][1]\n\n[1]: javascript:alert(1)'],
    ['protocol-relative', '[x](//evil.example/path)'],
  ])('%s gets no live link', (_name, payload) => {
    const html = renderMarkdown(payload);
    expect(html).not.toMatch(/href="\s*(javascript|vbscript|data|file):/i);
    expect(html).not.toMatch(/href="\/\//);
    expect(anchors(html)).toEqual([]);
    expect(hasScriptElement(html)).toBe(false);
  });

  it('a percent-encoded colon is a harmless relative path, not a scheme', () => {
    const html = renderMarkdown('[x](javascript%3Aalert(1))');
    expect(html).not.toMatch(/href="javascript:/i);
    expect(hasEventHandlerAttribute(html)).toBe(false);
  });

  it('backslash forms are percent-encoded by the parser, so they stay on this site', () => {
    for (const payload of ['[x](\\\\evil.example\\path)', '[x](/\\evil.example)']) {
      const href = /href="([^"]*)"/.exec(renderMarkdown(payload))?.[1];
      expect(href, payload).toBeDefined();
      expect(href).not.toMatch(/^\/\//);
      expect(href).not.toContain('\\'); // no raw backslash left for a browser to treat as a slash
      expect(href).not.toMatch(/^[a-z][a-z0-9+.-]*:/i);
    }
  });

  it('an image is never rendered (no data: or remote images)', () => {
    for (const payload of ['![x](https://evil.example/a.png)', '![x](javascript:alert(1))', '![x](data:image/png;base64,AAAA)']) {
      expect(renderMarkdown(payload)).not.toMatch(/<img/i);
    }
  });

  it('a title attribute cannot break out into an event handler', () => {
    const html = renderMarkdown('[x](https://ok.example "\\" onmouseover=\\"alert(1)")');
    expect(hasEventHandlerAttribute(html)).toBe(false);
    expect(attributeNames(html)).not.toContain('onmouseover');
    expect(html).toContain('title="&quot; onmouseover=&quot;alert(1)"'); // the payload is just escaped text inside the title
  });

  it('keeps safe links and gives every one rel="noopener"', () => {
    const html = renderMarkdown('[a](https://example.com) [b](http://example.com/x) [c](mailto:a@b.test) [d](tel:+982112345678) [e](/returns) [f](#top)');
    const found = anchors(html);
    expect(found).toHaveLength(6);
    for (const anchor of found) expect(anchor).toContain('rel="noopener');
  });

  it('external links open in a new tab with noreferrer and nofollow; internal ones do not', () => {
    const [external, internal] = anchors(renderMarkdown('[a](https://example.com) [b](/about)'));
    expect(external).toContain('rel="noopener noreferrer nofollow"');
    expect(external).toContain('target="_blank"');
    expect(internal).toContain('rel="noopener"');
    expect(internal).not.toContain('target=');
  });

  it('bare URLs are not auto-linked', () => {
    expect(anchors(renderMarkdown('see https://example.com now'))).toEqual([]);
  });
});

describe('isSafeLink', () => {
  it.each(['https://a.test', 'HTTP://A.TEST', 'mailto:a@b.test', 'tel:123', '/path', 'relative/path', '#anchor', '?q=1', 'a/b', '/a/b'])('allows %s', (url) =>
    expect(isSafeLink(url)).toBe(true),
  );

  it.each(['javascript:alert(1)', ' JavaScript:alert(1)', 'java\nscript:alert(1)', 'java\u0000script:x', 'data:text/html,x', 'vbscript:x', 'file:///x', '//evil.test', '', '   ', 'ftp://x.test', 'blob:https://x'])(
    'refuses %j',
    (url) => expect(isSafeLink(url)).toBe(false),
  );
});

describe('plainDescription', () => {
  it('uses the first real paragraph, without the draft banner, headings or markup', () => {
    const body = '> **[پیش‌نویس — بازبینی شود]** متن\n\n## عنوان\n\nاین یک [پیوند](https://a.test) و **متن پررنگ** است.\n\nپاراگراف دوم.';
    expect(plainDescription(body)).toBe('این یک پیوند و متن پررنگ است.');
  });

  it('truncates long text with an ellipsis and handles empty input', () => {
    expect(plainDescription('الف '.repeat(100), 50).length).toBeLessThanOrEqual(50);
    expect(plainDescription('الف '.repeat(100), 50).endsWith('…')).toBe(true);
    expect(plainDescription('')).toBe('');
    expect(plainDescription('> فقط بنر')).toBe('');
  });
});
