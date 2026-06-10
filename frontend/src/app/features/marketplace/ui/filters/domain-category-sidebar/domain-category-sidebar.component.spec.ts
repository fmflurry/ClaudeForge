/**
 * Unit tests for DomainCategorySidebarComponent
 *
 * Tests radio single-select behavior for domain category filtering.
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { DomainCategorySidebarComponent } from './domain-category-sidebar.component';
import type { DomainCategory } from './domain-category-sidebar.component';

function makeCategories(): readonly DomainCategory[] {
  return [
    { id: 'code-intelligence', label: 'Code Intelligence', count: 5 },
    { id: 'testing-qa', label: 'Testing & QA', count: 3 },
    { id: 'security', label: 'Security', count: 1 },
  ];
}

function setup(): {
  fixture: ComponentFixture<DomainCategorySidebarComponent>;
  comp: DomainCategorySidebarComponent;
} {
  TestBed.configureTestingModule({
    imports: [DomainCategorySidebarComponent],
  });
  const fixture = TestBed.createComponent(DomainCategorySidebarComponent);
  fixture.componentRef.setInput('categories', makeCategories());
  const comp = fixture.componentInstance;
  return { fixture, comp };
}

describe('DomainCategorySidebarComponent', () => {
  it('creates instance', () => {
    const { comp } = setup();
    expect(comp).toBeDefined();
  });

  it('emits categorySelected when onCategorySelect called', () => {
    const { comp } = setup();
    const emitted: string[] = [];
    comp.categorySelected.subscribe((id) => emitted.push(id));

    comp.onCategorySelect('code-intelligence');
    expect(emitted).toEqual(['code-intelligence']);
  });

  it('emits different category on second call', () => {
    const { comp } = setup();
    const emitted: string[] = [];
    comp.categorySelected.subscribe((id) => emitted.push(id));

    comp.onCategorySelect('code-intelligence');
    comp.onCategorySelect('testing-qa');
    expect(emitted).toEqual(['code-intelligence', 'testing-qa']);
  });
});
