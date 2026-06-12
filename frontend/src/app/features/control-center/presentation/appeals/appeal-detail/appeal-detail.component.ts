import { ChangeDetectionStrategy, Component, inject, OnInit, input, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AppealsFacade } from '../../../application/facades/appeals.facade';

@Component({
  selector: 'cf-appeal-detail',
  standalone: true,
  providers: [AppealsFacade],
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Appeal Detail</h1>
    <p><a routerLink="/control-center/appeals">Back to Appeals</a></p>
    @if (facade.isLoadingDetail()) {
      <p>Loading appeal details...</p>
    } @else if (facade.appealDetail(); as detail) {
      <div class="detail-card">
        <h2>Appeal Info</h2>
        <p><strong>Plugin:</strong> {{ detail.pluginName ?? detail.pluginId }}</p>
        <p><strong>Reason:</strong> {{ detail.reason }}</p>
        <p><strong>Status:</strong> {{ detail.status }}</p>
        @if (detail.evidence) {
          <p><strong>Evidence:</strong> {{ detail.evidence }}</p>
        }
      </div>
      @if (detail.analysisResult; as result) {
        <div class="detail-card">
          <h2>Analysis Results</h2>
          <p><strong>Total Score:</strong> {{ result.totalScore }}</p>
          <p><strong>Status:</strong> {{ result.status }}</p>
          <p><strong>Eslint:</strong> {{ result.staticScores.eslint }}</p>
          <p><strong>Semgrep:</strong> {{ result.staticScores.semgrep }}</p>
          <p><strong>Gitleaks:</strong> {{ result.staticScores.gitleaks }}</p>
          <p><strong>Trivy:</strong> {{ result.staticScores.trivy }}</p>
          <p><strong>Dynamic:</strong> {{ result.dynamicScore }}</p>
        </div>
      }
      @if (detail.status === 'pending') {
        <div class="detail-card">
          <h2>Resolution</h2>
          <textarea
            [value]="resolutionNotes()"
            (input)="resolutionNotes.set($any($event.target).value)"
            placeholder="Resolution notes..."
            rows="3"
            style="width:100%"
          ></textarea>
          <div class="actions">
            <button (click)="approve(detail.appealId)">Approve</button>
            <button (click)="reject(detail.appealId)">Reject</button>
          </div>
        </div>
      }
    }
  `,
  styles: [
    `
      .detail-card {
        background: var(--card);
        border: 1px solid var(--border);
        border-radius: 0.5rem;
        padding: 1.25rem;
        margin-bottom: 1rem;
      }
      .actions {
        display: flex;
        gap: 0.5rem;
        margin-top: 1rem;
      }
      button {
        padding: 0.5rem 1rem;
        border: 1px solid var(--border);
        border-radius: 0.25rem;
        cursor: pointer;
        background: var(--primary);
        color: var(--primary-foreground);
      }
    `,
  ],
})
export class AppealDetailComponent implements OnInit {
  readonly facade = inject(AppealsFacade);
  readonly appealId = input.required<string>();
  readonly resolutionNotes = signal('');

  ngOnInit(): void {
    this.facade.loadAppealDetail(this.appealId());
  }

  approve(id: string): void {
    if (confirm('Approve this appeal?')) {
      this.facade.resolveAppeal(id, 'approved', this.resolutionNotes() || undefined);
    }
  }

  reject(id: string): void {
    if (confirm('Reject this appeal?')) {
      this.facade.resolveAppeal(id, 'rejected', this.resolutionNotes() || undefined);
    }
  }
}
