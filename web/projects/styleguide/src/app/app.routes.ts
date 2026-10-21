import { Routes } from '@angular/router';
import { TokensPage } from './pages/tokens.page';
import { ComponentsPage } from './pages/components.page';
import { LayoutPage } from './pages/layout.page';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'tokens' },
  { path: 'tokens', component: TokensPage, title: 'توکن‌ها' },
  { path: 'components', component: ComponentsPage, title: 'کامپوننت‌ها' },
  { path: 'layout', component: LayoutPage, title: 'هدر و فوتر' },
  { path: '**', redirectTo: 'components' },
];
