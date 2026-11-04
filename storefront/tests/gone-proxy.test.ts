import { NextRequest } from 'next/server';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { proxy } from '../src/proxy';

// The proxy also asks the API about the analytics id for the CSP; these tests count the API calls for the 410 lookup only.
vi.mock('../src/lib/analytics-config', () => ({ analyticsCspExtra: async () => undefined }));

const request = (path: string, method = 'GET') => new NextRequest(`http://localhost:3000${path}`, { method });

const answer = (kind: string) => new Response(JSON.stringify({ result: { kind } }), { status: 200, headers: { 'content-type': 'application/json' } });

describe('410 for permanently deleted paths (decision C-3)', () => {
  const fetchMock = vi.fn();

  beforeEach(() => {
    fetchMock.mockReset();
    vi.stubGlobal('fetch', fetchMock);
    vi.stubEnv('API_BASE_URL', 'http://api.test');
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.unstubAllEnvs();
  });

  it('rewrites a deleted entity\'s path to the /gone page with status 410', async () => {
    fetchMock.mockResolvedValue(answer('gone'));

    const response = await proxy(request('/blog/deleted-post'));

    expect(response.status).toBe(410);
    expect(response.headers.get('x-middleware-rewrite')).toContain('/gone');
    expect(String(fetchMock.mock.calls[0][0])).toBe('http://api.test/api/v1/catalog/resolve?path=%2Fblog%2Fdeleted-post');
  });

  it('leaves everything else alone: entities, redirects and unknown paths are the page\'s business', async () => {
    for (const kind of ['product', 'redirect', 'notFound']) {
      fetchMock.mockResolvedValue(answer(kind));
      const response = await proxy(request(`/shop/${kind}-x`));
      expect(response.status, kind).toBe(200);
      expect(response.headers.get('x-middleware-rewrite'), kind).toBeNull();
    }
  });

  it('does not ask about static routes, the admin, malformed paths or non-GET requests', async () => {
    fetchMock.mockResolvedValue(answer('gone'));

    for (const path of ['/cart', '/order/abc', '/search', '/blog', '/wp-login.php', '/a//b', '/']) {
      expect((await proxy(request(path))).status, path).toBe(200);
    }
    expect((await proxy(request('/shop/post-only', 'POST'))).status).toBe(200);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('a failing or slow API never turns a page into a 410, and lookups pause briefly instead of slowing every request', async () => {
    vi.useFakeTimers();
    try {
      vi.setSystemTime(new Date('2026-11-04T09:00:00Z'));
      fetchMock.mockRejectedValueOnce(new Error('timeout'));
      expect((await proxy(request('/shop/api-down'))).status).toBe(200);

      expect((await proxy(request('/shop/api-down-too'))).status).toBe(200); // paused: no second 1.5 s wait
      expect(fetchMock).toHaveBeenCalledTimes(1);

      vi.setSystemTime(new Date('2026-11-04T09:00:06Z'));
      fetchMock.mockResolvedValueOnce(new Response('boom', { status: 500 }));
      expect((await proxy(request('/shop/api-500'))).status).toBe(200);
      expect(fetchMock).toHaveBeenCalledTimes(2);
    } finally {
      vi.useRealTimers();
    }
  });

  it('remembers "gone" (permanent) for a minute, but never "not gone": a just-deleted post answers 410 at once', async () => {
    vi.useFakeTimers();
    try {
      vi.setSystemTime(new Date('2026-11-04T10:00:00Z'));
      fetchMock.mockResolvedValueOnce(answer('blogPost'));
      expect((await proxy(request('/blog/about-to-go'))).status).toBe(200);

      vi.setSystemTime(new Date('2026-11-04T10:00:01Z')); // deleted one second later
      fetchMock.mockResolvedValueOnce(answer('gone'));
      expect((await proxy(request('/blog/about-to-go'))).status).toBe(410);

      vi.setSystemTime(new Date('2026-11-04T10:00:30Z')); // gone is remembered: no new call
      expect((await proxy(request('/blog/about-to-go'))).status).toBe(410);
      expect(fetchMock).toHaveBeenCalledTimes(2);
    } finally {
      vi.useRealTimers();
    }
  });
});
