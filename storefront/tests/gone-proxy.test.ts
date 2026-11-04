import { NextRequest } from 'next/server';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { proxy } from '../src/proxy';

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

  it('a failing or slow API never turns a page into a 410', async () => {
    fetchMock.mockRejectedValueOnce(new Error('timeout'));
    expect((await proxy(request('/shop/api-down'))).status).toBe(200);

    fetchMock.mockResolvedValueOnce(new Response('boom', { status: 500 }));
    expect((await proxy(request('/shop/api-500'))).status).toBe(200);
  });

  it('remembers an answer for a short time instead of asking again', async () => {
    fetchMock.mockResolvedValue(answer('gone'));

    await proxy(request('/shop/cached-gone'));
    await proxy(request('/shop/cached-gone'));

    expect(fetchMock).toHaveBeenCalledTimes(1);
  });
});
