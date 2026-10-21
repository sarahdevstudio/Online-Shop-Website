import { Directive, ElementRef, effect, model, viewChild } from '@angular/core';

let nextDialogId = 0;

/**
 * Shared behaviour for Modal and Drawer, built on the native <dialog> element:
 * focus is trapped and restored by the browser, Escape closes, the page behind becomes inert,
 * and the dialog renders in the top layer (no z-index wars).
 */
@Directive()
export abstract class UiDialogBase {
  /** Two-way: [(open)]="isOpen". Closing from inside (Escape, backdrop, close button) updates it. */
  readonly open = model(false);

  protected readonly titleId = `ui-dialog-title-${++nextDialogId}`;
  protected readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  constructor() {
    effect(() => {
      const element = this.dialog().nativeElement;
      const shouldBeOpen = this.open();
      if (shouldBeOpen && !element.open) {
        element.showModal();
      } else if (!shouldBeOpen && element.open) {
        element.close();
      }
    });
  }

  /** Fired by the native <dialog> after it closes for any reason. */
  protected onClosed(): void {
    this.open.set(false);
  }

  /** A click whose target is the <dialog> itself landed on the backdrop (the panel fills the rest). */
  protected onBackdropClick(event: MouseEvent): void {
    if (event.target === this.dialog().nativeElement) {
      this.open.set(false);
    }
  }
}
