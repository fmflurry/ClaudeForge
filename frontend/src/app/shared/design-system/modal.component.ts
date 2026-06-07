import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  inject,
  input,
  output,
  viewChild,
} from '@angular/core';
import { I18nFacade } from '../../application/i18n/i18n.facade';

@Component({
  selector: 'cf-modal',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div
      class="cf-modal__backdrop"
      role="dialog"
      aria-modal="true"
      [attr.aria-label]="title()"
      (click)="onBackdropClick($event)"
      (keydown.escape)="modalClose.emit()"
    >
      <div class="cf-modal__panel" #panel>
        <header class="cf-modal__header">
          <h2 class="cf-modal__title">{{ title() }}</h2>
          <button
            class="cf-modal__close"
            [attr.aria-label]="i18n.t('shared.modal.close-aria')"
            (click)="modalClose.emit()"
          >
            &times;
          </button>
        </header>
        <div class="cf-modal__body">
          <ng-content />
        </div>
      </div>
    </div>
  `,
  styles: [
    `
      .cf-modal__backdrop {
        position: fixed;
        inset: 0;
        background: rgba(0, 0, 0, 0.5);
        display: flex;
        align-items: center;
        justify-content: center;
        z-index: 1000;
      }

      .cf-modal__panel {
        background: #fff;
        border-radius: 0.5rem;
        box-shadow: 0 20px 25px -5px rgb(0 0 0 / 0.1);
        width: 100%;
        max-width: 36rem;
        max-height: 90vh;
        overflow-y: auto;
      }

      .cf-modal__header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        padding: 1rem 1.25rem;
        border-bottom: 1px solid #e5e7eb;
      }

      .cf-modal__title {
        font-size: 1rem;
        font-weight: 600;
        margin: 0;
        color: #111827;
      }

      .cf-modal__close {
        background: transparent;
        border: none;
        font-size: 1.25rem;
        cursor: pointer;
        color: #6b7280;
        line-height: 1;
        padding: 0.25rem;
      }

      .cf-modal__close:hover {
        color: #111827;
      }

      .cf-modal__body {
        padding: 1.25rem;
      }
    `,
  ],
})
export class ModalComponent {
  protected readonly i18n = inject(I18nFacade);

  readonly title = input.required<string>();
  /** Emitted when the user requests to close the modal (Escape, backdrop click, close button). */
  readonly modalClose = output<void>();

  private readonly panelRef = viewChild<ElementRef<HTMLDivElement>>('panel');

  @HostListener('document:keydown.escape')
  onEscKey(): void {
    this.modalClose.emit();
  }

  onBackdropClick(event: MouseEvent): void {
    const panel = this.panelRef()?.nativeElement;
    if (panel && !panel.contains(event.target as Node)) {
      this.modalClose.emit();
    }
  }
}
