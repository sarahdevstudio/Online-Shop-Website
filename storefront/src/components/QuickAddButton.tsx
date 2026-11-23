'use client';

import { useRouter } from 'next/navigation';
import { useState, useTransition } from 'react';
import { quickAddToCartAction } from '@/app/(shop)/cart/actions';
import { Button } from './Button';
import { Icon } from './Icon';
import { messages } from '@/lib/messages';
import styles from './QuickAddButton.module.css';

/** One click adds the product's only variant to the cart, without leaving the page (docs/10 home redesign). */
export function QuickAddButton({ variantId }: { variantId: number }) {
  const [pending, startTransition] = useTransition();
  const [status, setStatus] = useState<'idle' | 'added' | 'error'>('idle');
  const router = useRouter();

  const onClick = () => {
    startTransition(async () => {
      const result = await quickAddToCartAction(variantId);
      if (result.ok) {
        setStatus('added');
        router.refresh();
        setTimeout(() => setStatus('idle'), 2000);
      } else {
        setStatus('error');
      }
    });
  };

  return (
    <>
      <Button type="button" variant="secondary" size="sm" iconOnly className={styles.button} loading={pending} onClick={onClick} aria-label={messages.addToCart}>
        <Icon name={status === 'added' ? 'check' : 'cart'} />
      </Button>
      <p role="status" aria-live="polite" className="ui-visually-hidden">
        {status === 'added' ? messages.addedToCart : status === 'error' ? messages.cartErrors.invalid : ''}
      </p>
    </>
  );
}
