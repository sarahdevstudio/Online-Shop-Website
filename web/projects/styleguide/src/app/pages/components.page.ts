import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import {
  BreadcrumbItem,
  ICONS,
  IconName,
  UiAlert,
  UiBadge,
  UiBreadcrumb,
  UiButton,
  UiCheck,
  UiControl,
  UiDrawer,
  UiEmptyState,
  UiErrorState,
  UiField,
  UiIcon,
  UiModal,
  UiPagination,
  UiPrice,
  UiProductCard,
  UiProductCardSkeleton,
  UiQuantityStepper,
  UiRating,
  UiSkeleton,
  UiSpinner,
} from 'shared-ui';
import { SAMPLE_PRODUCTS } from '../sample-data';

@Component({
  selector: 'sg-components-page',
  imports: [
    ReactiveFormsModule,
    UiAlert,
    UiBadge,
    UiBreadcrumb,
    UiButton,
    UiCheck,
    UiControl,
    UiDrawer,
    UiEmptyState,
    UiErrorState,
    UiField,
    UiIcon,
    UiModal,
    UiPagination,
    UiPrice,
    UiProductCard,
    UiProductCardSkeleton,
    UiQuantityStepper,
    UiRating,
    UiSkeleton,
    UiSpinner,
  ],
  templateUrl: './components.page.html',
  styleUrl: './components.page.css',
})
export class ComponentsPage {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);

  protected readonly products = SAMPLE_PRODUCTS;
  protected readonly iconNames = Object.keys(ICONS) as IconName[];
  protected readonly breadcrumb: BreadcrumbItem[] = [
    { label: 'خانه', link: '/' },
    { label: 'لوستر', link: '/components' },
    { label: 'لوستر کریستال', link: '/components' },
    { label: 'لوستر کریستال ۱۲ شاخه مدل ورسای' },
  ];

  protected readonly quantity = signal(2);
  protected readonly modalOpen = signal(false);
  protected readonly drawerOpen = signal(false);
  protected readonly drawerStartOpen = signal(false);
  protected readonly loadingDemo = signal(false);
  protected readonly alertVisible = signal(true);
  protected readonly buttonPage = signal(4);
  protected readonly submitted = signal(false);

  protected readonly routedPage = computed(() => 1);

  protected readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    phone: ['', [Validators.required, Validators.pattern(/^(\+98|0)?9\d{9}$/)]],
    city: ['', Validators.required],
    address: ['', [Validators.required, Validators.minLength(10)]],
    terms: [false, Validators.requiredTrue],
  });

  protected errorFor(name: 'email' | 'phone' | 'city' | 'address' | 'terms'): string | null {
    const control = this.form.controls[name];
    if (!(control.touched || this.submitted()) || control.valid) {
      return null;
    }
    const errors = control.errors ?? {};
    if (errors['required'] || errors['requiredTrue']) return 'این فیلد الزامی است.';
    if (errors['email']) return 'ایمیل واردشده معتبر نیست.';
    if (errors['pattern']) return 'شماره موبایل معتبر نیست (مثل ۰۹۱۲۱۲۳۴۵۶۷).';
    if (errors['minlength']) return 'آدرس باید دست‌کم ۱۰ نویسه باشد.';
    return 'مقدار نامعتبر است.';
  }

  protected submit(): void {
    this.submitted.set(true);
    this.form.markAllAsTouched();
  }

  protected simulateLoading(): void {
    this.loadingDemo.set(true);
    setTimeout(() => this.loadingDemo.set(false), 1800);
  }

  protected currentPage(): number {
    const raw = Number(this.route.snapshot.queryParamMap.get('page'));
    return Number.isInteger(raw) && raw > 0 ? raw : 1;
  }
}
