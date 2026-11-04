import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const siteWith = (ga4Id: unknown) => new Response(JSON.stringify({ result: { seo: { ga4Id } } }), { status: 200, headers: { 'content-type': 'application/json' } });

describe('the CSP names Google only while a GA4 id is configured', () => {
  const fetchMock = vi.fn();
  beforeEach(() => {
    vi.resetModules();
    fetchMock.mockReset();
    vi.stubGlobal('fetch', fetchMock);
    vi.stubEnv('API_BASE_URL', 'http://api.test');
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.unstubAllEnvs();
  });

  it('adds the hosts for a valid id, and remembers the answer for a while', async () => {
    fetchMock.mockResolvedValue(siteWith('G-ABC123XYZ'));
    const { analyticsCspExtra } = await import('../src/lib/analytics-config');
    const extra = await analyticsCspExtra();
    expect(extra?.script).toEqual(['https://www.googletagmanager.com']);
    expect(extra?.connect).toContain('https://www.google-analytics.com');
    await analyticsCspExtra();
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it('adds nothing when no id is set, the id is malformed, or the API cannot be reached', async () => {
    for (const body of [siteWith(''), siteWith('UA-1-1'), siteWith(undefined)]) {
      vi.resetModules();
      fetchMock.mockResolvedValue(body);
      const { analyticsCspExtra } = await import('../src/lib/analytics-config');
      expect(await analyticsCspExtra()).toBeUndefined();
    }
    vi.resetModules();
    fetchMock.mockRejectedValue(new Error('down'));
    const { analyticsCspExtra } = await import('../src/lib/analytics-config');
    expect(await analyticsCspExtra()).toBeUndefined();
  });
});
