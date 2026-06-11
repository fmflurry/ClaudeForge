import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { ConfigFacade } from '../../application/facades/config.facade';

@Component({
  selector: 'cf-config-dashboard',
  standalone: true,
  providers: [ConfigFacade],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Analysis Configuration</h1>
    @if (facade.isSaving()) {
      <p>Saving...</p>
    }
    @if (facade.error(); as err) {
      <p style="color: var(--destructive)">{{ err[0].message }}</p>
    }
    @if (facade.analysisConfig(); as config) {
      <form (ngSubmit)="save()" class="config-form">
        <div class="field">
          <label for="staticWeight">Static Weight ({{ formValues().staticWeight }})</label>
          <input id="staticWeight" type="range" min="0" max="1" step="0.05"
            [value]="formValues().staticWeight"
            (input)="updateField('staticWeight', +$any($event.target).value)" />
        </div>
        <div class="field">
          <label for="dynamicWeight">Dynamic Weight ({{ formValues().dynamicWeight }})</label>
          <input id="dynamicWeight" type="range" min="0" max="1" step="0.05"
            [value]="formValues().dynamicWeight"
            (input)="updateField('dynamicWeight', +$any($event.target).value)" />
        </div>
        <div class="field">
          <label for="passThreshold">Pass Threshold</label>
          <input id="passThreshold" type="number" [value]="formValues().passThreshold"
            (input)="updateField('passThreshold', +$any($event.target).value)" />
        </div>
        <div class="field">
          <label for="failThreshold">Fail Threshold</label>
          <input id="failThreshold" type="number" [value]="formValues().failThreshold"
            (input)="updateField('failThreshold', +$any($event.target).value)" />
        </div>
        <div class="field">
          <label for="maxWorkers">Max Workers</label>
          <input id="maxWorkers" type="number" [value]="formValues().maxWorkers"
            (input)="updateField('maxWorkers', +$any($event.target).value)" />
        </div>
        <div class="field">
          <label for="retryLimit">Retry Limit</label>
          <input id="retryLimit" type="number" [value]="formValues().retryLimit"
            (input)="updateField('retryLimit', +$any($event.target).value)" />
        </div>
        <button type="submit">Save Configuration</button>
      </form>
    }

    <div class="section">
      <h2>Change History</h2>
      @if (facade.configHistory().length > 0) {
        <table class="table">
          <thead><tr><th>Date</th><th>Description</th><th>Changed By</th></tr></thead>
          <tbody>
            @for (h of facade.configHistory(); track h.id) {
              <tr><td>{{ h.createdAt }}</td><td>{{ h.changeDescription }}</td><td>{{ h.changedBy }}</td></tr>
            }
          </tbody>
        </table>
      } @else {
        <p>No history available.</p>
      }
    </div>
  `,
  styles: [`
    .config-form { max-width: 500px; }
    .field { margin-bottom: 1rem; }
    .field label { display: block; margin-bottom: 0.25rem; font-weight: 600; }
    .field input { width: 100%; }
    .section { margin-top: 2rem; }
    .table { width: 100%; border-collapse: collapse; }
    .table th, .table td { text-align: left; padding: 0.5rem; border-bottom: 1px solid var(--border); }
    button { padding: 0.5rem 1rem; border: 1px solid var(--border); border-radius: 0.25rem; cursor: pointer; background: var(--primary); color: var(--primary-foreground); margin-top: 1rem; }
  `],
})
export class ConfigDashboardComponent implements OnInit {
  readonly facade = inject(ConfigFacade);
  readonly formValues = signal({
    staticWeight: 0.6,
    dynamicWeight: 0.4,
    passThreshold: 80,
    failThreshold: 50,
    maxWorkers: 2,
    retryLimit: 3,
  });

  ngOnInit(): void {
    this.facade.loadConfig();
    this.facade.loadHistory();
  }

  updateField(field: string, value: number): void {
    this.formValues.update((v) => ({ ...v, [field]: value }));
  }

  save(): void {
    this.facade.updateConfig({ ...this.formValues() });
  }
}
