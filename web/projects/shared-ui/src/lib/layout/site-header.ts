import { booleanAttribute, ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { UiDrawer } from '../overlay/drawer';
import { UiIcon } from '../icon/icon';
import { UiControl } from '../form/control';
import { formatNumber } from '../core/format';
import { injectUiMessages } from '../core/ui-messages';

export interface NavItem {
  label: string;
  link: string;
}

/**
 * Storefront header: skip link, brand, search, account, cart and primary navigation.
 * Phone: [menu] [brand] [account cart] with the search bar on its own row; navigation opens in a drawer.
 * Desktop (>=1024px): brand | search | actions, with the navigation bar underneath.
 *
 * The page must contain <main id="main" tabindex="-1"> as the skip-link target.
 */
@Component({
  selector: 'ui-site-header',
  imports: [RouterLink, RouterLinkActive, UiDrawer, UiIcon, UiControl],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="header">
      <a class="ui-skip-link" href="#main">{{ messages.skipToContent }}</a>
      <div class="ui-container bar">
        <button type="button" class="icon-btn menu-btn" [attr.aria-label]="messages.menu" aria-haspopup="dialog" (click)="menuOpen.set(true)">
          <ui-icon name="menu" />
        </button>

        <a class="brand" [routerLink]="homeLink()">
          @if (logoSrc()) {
            <img [src]="logoSrc()" alt="" width="36" height="36" />
          }
          <span>{{ brandName() }}</span>
        </a>

        <form class="search" role="search" (submit)="onSubmit($event, query)">
          <label class="ui-visually-hidden" for="ui-header-search">{{ messages.searchLabel }}</label>
          <input #query id="ui-header-search" uiControl type="search" name="q" autocomplete="off" enterkeyhint="search" [placeholder]="messages.searchPlaceholder" />
          <button type="submit" class="search-btn" [attr.aria-label]="messages.searchSubmit">
            <ui-icon name="search" />
          </button>
        </form>

        <div class="actions">
          @if (showThemeToggle()) {
            <button type="button" class="icon-btn" [attr.aria-label]="messages.theme" (click)="themeToggle.emit()">
              <ui-icon name="moon" />
            </button>
          }
          <a class="icon-btn account" [routerLink]="accountLink()" [attr.aria-label]="accountLabel() ?? messages.account">
            <ui-icon name="user" />
            @if (accountLabel()) {
              <span class="account-name">{{ accountLabel() }}</span>
            }
          </a>
          <a class="icon-btn cart" [routerLink]="cartLink()" [attr.aria-label]="cartLabel()">
            <ui-icon name="cart" />
            @if (cartCount() > 0) {
              <span class="count" aria-hidden="true">{{ cartCountText() }}</span>
            }
          </a>
        </div>
      </div>

      <nav class="primary" [attr.aria-label]="messages.mainNavigation">
        <ul class="ui-container">
          @for (item of navItems(); track item.link) {
            <li>
              <a [routerLink]="item.link" routerLinkActive="active" ariaCurrentWhenActive="page">{{ item.label }}</a>
            </li>
          }
        </ul>
      </nav>
    </header>

    <ui-drawer [(open)]="menuOpen" [heading]="messages.menu" side="start">
      <nav [attr.aria-label]="messages.mainNavigation">
        <ul class="drawer-nav">
          @for (item of navItems(); track item.link) {
            <li>
              <a [routerLink]="item.link" routerLinkActive="active" ariaCurrentWhenActive="page" (click)="menuOpen.set(false)">{{ item.label }}</a>
            </li>
          }
        </ul>
      </nav>
    </ui-drawer>
  `,
  styleUrl: './site-header.css',
})
export class UiSiteHeader {
  readonly brandName = input.required<string>();
  readonly logoSrc = input<string | null>();
  readonly homeLink = input('/');
  readonly navItems = input<readonly NavItem[]>([]);
  readonly cartLink = input('/cart');
  readonly cartCount = input(0);
  readonly accountLink = input('/account');
  /** Signed-in customer's name; omit when signed out. */
  readonly accountLabel = input<string | null>();
  readonly showThemeToggle = input(false, { transform: booleanAttribute });

  readonly search = output<string>();
  readonly themeToggle = output<void>();

  protected readonly messages = injectUiMessages();
  protected readonly menuOpen = signal(false);

  protected readonly cartCountText = computed(() => (this.cartCount() > 99 ? `${formatNumber(99)}+` : formatNumber(this.cartCount())));
  protected readonly cartLabel = computed(() =>
    this.cartCount() > 0 ? this.messages.cartWithCount(formatNumber(this.cartCount())) : this.messages.cart,
  );

  protected onSubmit(event: Event, input: HTMLInputElement): void {
    event.preventDefault();
    const value = input.value.trim();
    if (value) {
      this.search.emit(value);
    }
  }
}
