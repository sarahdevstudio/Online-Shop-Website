import { NextResponse } from 'next/server';
import { apiGet } from '@/lib/api';

/**
 * Suggestions for the header search box. A thin pass-through to the API's typo-tolerant index (the browser never talks to the API
 * directly); the answer may be cached for a few seconds, since many visitors type the same first letters. Any trouble is an empty
 * answer: the search box must never break a page.
 */
const EMPTY = { products: [], categories: [], fuzzy: false };

export async function GET(request: Request) {
  const q = new URL(request.url).searchParams.get('q')?.trim().slice(0, 100) ?? '';
  if (q.length < 2) return NextResponse.json(EMPTY);
  try {
    const result = await apiGet<unknown>(`/catalog/suggest?q=${encodeURIComponent(q)}`, 30);
    return NextResponse.json(result ?? EMPTY, { headers: { 'cache-control': 'public, max-age=15, s-maxage=30' } });
  } catch {
    return NextResponse.json(EMPTY);
  }
}
