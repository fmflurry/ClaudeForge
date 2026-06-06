import { ChangeDetectionStrategy, Component } from '@angular/core';

@Component({
  selector: 'cf-docs-page',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<h2>Docs</h2>`,
})
export class DocsPageComponent {}
