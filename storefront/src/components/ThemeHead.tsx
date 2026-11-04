import { fontFaceCss, fontPreloads, themeCss } from '@/lib/theme-css';
import type { SiteTheme } from '@/lib/site-config';

/**
 * The one place the theme `<style>` is emitted. The CSS is generated from validated typed values only (lib/theme-css), so no
 * admin text can reach it; keeping a single emitter lets a future CSP hash or nonce it.
 */
export function ThemeHead({ theme }: { theme: SiteTheme }) {
  const preloads = fontPreloads(theme.font);
  const css = `${fontFaceCss(theme.font)}${themeCss(theme)}`;
  return (
    <>
      {preloads.map((preload) => (
        <link key={preload.href} rel="preload" as="font" type={preload.type} href={preload.href} crossOrigin="anonymous" />
      ))}
      <style id="site-theme" dangerouslySetInnerHTML={{ __html: css }} />
    </>
  );
}
