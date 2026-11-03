import { describe, expect, it } from 'vitest';
import { enamadBadge, samandehiBadge } from '../src/lib/trust-badges';

describe('trust badges', () => {
  it('builds the e-Namad link and image from the identifiers only', () => {
    expect(enamadBadge({ id: '123456', code: 'AbCdEfGh12' })).toEqual({
      href: 'https://trustseal.enamad.ir/?id=123456&Code=AbCdEfGh12',
      src: 'https://trustseal.enamad.ir/logo.aspx?id=123456&Code=AbCdEfGh12',
    });
  });

  it('builds the Samandehi link and image from the id and the two codes', () => {
    expect(samandehiBadge({ id: '77', verifyCode: 'verifycode1', logoCode: 'logocode123' })).toEqual({
      href: 'https://logo.samandehi.ir/Verify.aspx?id=77&p=verifycode1',
      src: 'https://logo.samandehi.ir/logo.aspx?id=77&p=logocode123',
    });
  });

  it('renders nothing unless every identifier is well-formed', () => {
    expect(enamadBadge(null)).toBeNull();
    expect(enamadBadge({ id: '12', code: 'short' })).toBeNull();
    expect(enamadBadge({ id: '1&x=2', code: 'AbCdEfGh12' })).toBeNull();
    expect(enamadBadge({ id: '12', code: 'AbCdEfGh12"onerror="x' })).toBeNull();
    expect(samandehiBadge({ id: '77', verifyCode: 'verifycode1', logoCode: 'a b c d e f g h i' })).toBeNull();
    expect(samandehiBadge(null)).toBeNull();
  });

  it('only ever points at the two authority hosts', () => {
    const urls = [enamadBadge({ id: '1', code: 'AbCdEfGh12' }), samandehiBadge({ id: '1', verifyCode: 'AbCdEfGh12', logoCode: 'AbCdEfGh12' })].flatMap((b) => [b!.href, b!.src]);
    expect(urls.every((u) => ['trustseal.enamad.ir', 'logo.samandehi.ir'].includes(new URL(u).hostname))).toBe(true);
  });
});
