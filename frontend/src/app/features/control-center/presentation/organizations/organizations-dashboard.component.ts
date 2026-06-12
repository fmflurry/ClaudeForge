import { ChangeDetectionStrategy, Component, inject, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { OrganizationsFacade } from '../../application/facades/organizations.facade';

@Component({
  selector: 'cf-organizations-dashboard',
  standalone: true,
  providers: [OrganizationsFacade],
  imports: [RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <h1>Organizations</h1>
    @if (facade.isLoadingOrgs()) {
      <p>Loading...</p>
    } @else {
      <table class="table">
        <thead>
          <tr>
            <th>Name</th>
            <th>Slug</th>
            <th>Created</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          @for (org of facade.organizations(); track org.id) {
            <tr>
              <td>{{ org.name }}</td>
              <td>{{ org.slug }}</td>
              <td>{{ org.createdAt }}</td>
              <td><a [routerLink]="['/control-center/organizations', org.id]">View</a></td>
            </tr>
          } @empty {
            <tr>
              <td colspan="4">No organizations found</td>
            </tr>
          }
        </tbody>
      </table>
    }
  `,
  styles: [
    `
      .table {
        width: 100%;
        border-collapse: collapse;
      }
      .table th,
      .table td {
        text-align: left;
        padding: 0.5rem;
        border-bottom: 1px solid var(--border);
      }
    `,
  ],
})
export class OrganizationsDashboardComponent implements OnInit {
  readonly facade = inject(OrganizationsFacade);
  ngOnInit(): void {
    this.facade.loadOrganizations();
  }
}
