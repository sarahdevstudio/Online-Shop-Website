import { DOCUMENT } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { UiButton, UiIcon } from 'shared-ui';

interface AxeViolation {
  id: string;
  impact: string | null;
  help: string;
  nodes: number;
  targets: string[];
}

@Component({
  imports: [RouterOutlet, RouterLink, RouterLinkActive, UiButton, UiIcon],
  selector: 'sg-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {
  private readonly document = inject(DOCUMENT);

  protected readonly theme = signal<'light' | 'dark'>('light');
  protected readonly dir = signal<'rtl' | 'ltr'>('rtl');
  protected readonly axeRunning = signal(false);
  protected readonly axeViolations = signal<AxeViolation[] | null>(null);

  protected toggleTheme(): void {
    const next = this.theme() === 'light' ? 'dark' : 'light';
    this.theme.set(next);
    this.document.documentElement.setAttribute('data-theme', next);
  }

  protected toggleDir(): void {
    const next = this.dir() === 'rtl' ? 'ltr' : 'rtl';
    this.dir.set(next);
    this.document.documentElement.setAttribute('dir', next);
    this.document.documentElement.setAttribute('lang', next === 'rtl' ? 'fa' : 'en');
  }

  /** Dev-only accessibility audit of whatever is on screen (WCAG 2.x A/AA rules via axe-core). */
  protected async runAxe(): Promise<void> {
    this.axeRunning.set(true);
    try {
      const win = window as unknown as { axe?: AxeApi };
      if (!win.axe) {
        await new Promise<void>((resolve, reject) => {
          const script = this.document.createElement('script');
          script.src = '/axe.min.js';
          script.onload = () => resolve();
          script.onerror = () => reject(new Error('axe-core could not be loaded'));
          this.document.head.appendChild(script);
        });
      }
      const results = await win.axe!.run(this.document, {
        runOnly: { type: 'tag', values: ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa', 'wcag22aa', 'best-practice'] },
      });
      this.axeViolations.set(
        results.violations.map((v) => ({
          id: v.id,
          impact: v.impact,
          help: v.help,
          nodes: v.nodes.length,
          targets: v.nodes.slice(0, 3).map((n) => n.target.join(' ')),
        })),
      );
    } finally {
      this.axeRunning.set(false);
    }
  }
}

interface AxeApi {
  run(
    context: Document,
    options: unknown,
  ): Promise<{ violations: { id: string; impact: string | null; help: string; nodes: { target: string[] }[] }[] }>;
}
