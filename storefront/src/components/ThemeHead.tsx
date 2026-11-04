import { fontFaceCss, fontPreloads, themeCss } from '@/lib/theme-css';
import type { SiteTheme } from '@/lib/site-config';

/**
 * The one place the theme `<style>` is emitted. The CSS is generated from validated typed values only (lib/theme-css), so no
 * admin text can reach it; the CSP only allows this element through its per-request nonce.
 */
export function ThemeHead({ theme, nonce }: { theme: SiteTheme; nonce?: string }) {
  const preloads = fontPreloads(theme.font);
  const css = `${fontFaceCss(theme.font)}${themeCss(theme)}`;
  return (
    <>
      {preloads.map((preload) => (
        <link key={preload.href} rel="preload" as="font" type={preload.type} href={preload.href} crossOrigin="anonymous" />
      ))}
      <style id="site-theme" nonce={nonce} dangerouslySetInnerHTML={{ __html: css }} />
    </>
  );
}
