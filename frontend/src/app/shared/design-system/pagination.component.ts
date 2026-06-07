import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { I18nFacade } from '../../application/i18n/i18n.facade';

@Component({
  selector: 'cf-pagination',
  standalone: true,
  imports: [],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav class="cf-pagination" [attr.aria-label]="i18n.t('shared.pagination.aria')">
      <button
        class="cf-pagination__btn"
        [disabled]="currentPage() <= 1"
        (click)="onPageChange(currentPage() - 1)"
        [attr.aria-label]="i18n.t('shared.pagination.prev-aria')"
      >
        &laquo;
      </button>

      @for (page of pages(); track page) {
        <button
          class="cf-pagination__btn"
          [class.cf-pagination__btn--active]="page === currentPage()"
          (click)="onPageChange(page)"
          [attr.aria-current]="page === currentPage() ? 'page' : null"
        >
          {{ page }}
        </button>
      }

      <button
        class="cf-pagination__btn"
        [disabled]="currentPage() >= totalPages()"
        (click)="onPageChange(currentPage() + 1)"
        [attr.aria-label]="i18n.t('shared.pagination.next-aria')"
      >
        &raquo;
      </button>
    </nav>
  `,
  styles: [
    `
      .cf-pagination {
        display: flex;
        align-items: center;
        gap: 0.25rem;
      }

      .cf-pagination__btn {
        display: inline-flex;
        align-items: center;
        justify-content: center;
        width: 2rem;
        height: 2rem;
        border: 1px solid #d1d5db;
        background: #fff;
        border-radius: 0.25rem;
        cursor: pointer;
        font-size: 0.875rem;
        transition: background 0.15s ease;
      }

      .cf-pagination__btn:hover:not([disabled]) {
        background: #f3f4f6;
      }

      .cf-pagination__btn--active {
        background: #1a1a2e;
        color: #fff;
        border-color: #1a1a2e;
      }

      .cf-pagination__btn[disabled] {
        opacity: 0.4;
        cursor: not-allowed;
      }
    `,
  ],
})
export class PaginationComponent {
  protected readonly i18n = inject(I18nFacade);

  readonly currentPage = input.required<number>();
  readonly totalPages = input.required<number>();
  readonly pageChange = output<number>();

  readonly pages = computed<number[]>(() => {
    const total = this.totalPages();
    const current = this.currentPage();
    const delta = 2;
    const range: number[] = [];

    const start = Math.max(1, current - delta);
    const end = Math.min(total, current + delta);

    for (let i = start; i <= end; i++) {
      range.push(i);
    }
    return range;
  });

  onPageChange(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.pageChange.emit(page);
  }
}
