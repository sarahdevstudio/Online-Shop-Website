import type { ReactNode } from 'react';
import { ShopChrome } from '@/components/ShopChrome';

export default function ShopLayout({ children }: { children: ReactNode }) {
  return <ShopChrome>{children}</ShopChrome>;
}
