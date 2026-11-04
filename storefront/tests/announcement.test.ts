import { describe, expect, it } from 'vitest';
import { ANNOUNCEMENT_COOKIE, announcementKey, dismissalCookie, isAnnouncementDismissed } from '../src/lib/announcement';

describe('announcement dismissal', () => {
  it('has a stable key per text and a different one for a different text', () => {
    expect(announcementKey('ارسال رایگان')).toBe(announcementKey('ارسال رایگان'));
    expect(announcementKey('ارسال رایگان')).not.toBe(announcementKey('ارسال رایگان برای همه'));
    expect(announcementKey('')).toMatch(/^[0-9a-z]+$/);
  });

  it('is dismissed only for the text that was closed (a new announcement shows again)', () => {
    const closed = announcementKey('متن قدیمی');
    expect(isAnnouncementDismissed(closed, 'متن قدیمی')).toBe(true);
    expect(isAnnouncementDismissed(closed, 'متن جدید')).toBe(false);
    expect(isAnnouncementDismissed(undefined, 'متن قدیمی')).toBe(false);
  });

  it('writes a year-long, site-wide, lax cookie the server reads by the same name', () => {
    const cookie = dismissalCookie('متن');
    expect(cookie.startsWith(`${ANNOUNCEMENT_COOKIE}=${announcementKey('متن')};`)).toBe(true);
    expect(cookie).toContain('path=/');
    expect(cookie).toContain('max-age=31536000');
    expect(cookie.toLowerCase()).toContain('samesite=lax');
  });
});
