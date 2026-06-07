import { ChangeDetectionStrategy, Component, inject, input } from '@angular/core';
import { I18nFacade } from '../../application/i18n/i18n.facade';

export interface TableColumn<T> {
  key: keyof T;
  header: string;
}

@Component({
  selector: 'cf-table',
  standalone: true,
  imports: [],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cf-table-wrapper" role="region" [attr.aria-label]="ariaLabel() ?? i18n.t('shared.table.aria')">
      <table class="cf-table">
        <thead class="cf-table__head">
          <tr>
            @for (col of columns(); track col.key) {
              <th class="cf-table__th" scope="col">{{ col.header }}</th>
            }
          </tr>
        </thead>
        <tbody class="cf-table__body">
          @for (row of rows(); track $index) {
            <tr class="cf-table__row">
              @for (col of columns(); track col.key) {
                <td class="cf-table__td">{{ row[col.key] }}</td>
              }
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
  styles: [
    `
      .cf-table-wrapper {
        overflow-x: auto;
      }

      .cf-table {
        width: 100%;
        border-collapse: collapse;
        font-size: 0.875rem;
      }

      .cf-table__th {
        text-align: left;
        padding: 0.75rem 1rem;
        background: #f9fafb;
        border-bottom: 1px solid #e5e7eb;
        font-weight: 600;
        color: #374151;
        white-space: nowrap;
      }

      .cf-table__td {
        padding: 0.75rem 1rem;
        border-bottom: 1px solid #e5e7eb;
        color: #111827;
      }

      .cf-table__row:hover .cf-table__td {
        background: #f9fafb;
      }
    `,
  ],
})
export class TableComponent<T extends Record<string, unknown>> {
  protected readonly i18n = inject(I18nFacade);

  readonly columns = input.required<TableColumn<T>[]>();
  readonly rows = input.required<T[]>();
  readonly ariaLabel = input<string | undefined>(undefined);
}
