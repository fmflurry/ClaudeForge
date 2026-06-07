/**
 * FilterChipsComponent — render tests.
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChangeDetectionStrategy } from '@angular/core';
import { By } from '@angular/platform-browser';
import { FilterChipsComponent } from './filter-chips.component';

// ---------------------------------------------------------------------------
// Setup
// ---------------------------------------------------------------------------

function setup(opts: {
  activeTypes?: string[];
  activeLanguages?: string[];
  activeUseCases?: string[];
} = {}): ComponentFixture<FilterChipsComponent> {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [FilterChipsComponent],
  }).overrideComponent(FilterChipsComponent, {
    set: { changeDetection: ChangeDetectionStrategy.Default },
  });
  const fixture = TestBed.createComponent(FilterChipsComponent);
  if (opts.activeTypes !== undefined) {
    fixture.componentRef.setInput('activeTypes', opts.activeTypes);
  }
  if (opts.activeLanguages !== undefined) {
    fixture.componentRef.setInput('activeLanguages', opts.activeLanguages);
  }
  if (opts.activeUseCases !== undefined) {
    fixture.componentRef.setInput('activeUseCases', opts.activeUseCases);
  }
  fixture.detectChanges();
  return fixture;
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('FilterChipsComponent — render', () => {
  it('should instantiate', () => {
    const fixture = setup();
    expect(fixture.componentInstance).toBeDefined();
  });

  it('should render the filter-chips container', () => {
    const fixture = setup();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('.cf-filter-chips')).not.toBeNull();
  });

  it('should render type chips when activeTypes has values', () => {
    const fixture = setup({ activeTypes: ['formatter', 'linter'] });
    const chips = fixture.debugElement.queryAll(By.css('[data-testid="chip-type"]'));
    expect(chips).toHaveLength(2);
  });

  it('should render language chips when activeLanguages has values', () => {
    const fixture = setup({ activeLanguages: ['typescript', 'python'] });
    const chips = fixture.debugElement.queryAll(By.css('[data-testid="chip-language"]'));
    expect(chips).toHaveLength(2);
  });

  it('should render use-case chips when activeUseCases has values', () => {
    const fixture = setup({ activeUseCases: ['code-quality'] });
    const chips = fixture.debugElement.queryAll(By.css('[data-testid="chip-usecase"]'));
    expect(chips).toHaveLength(1);
  });

  it('should display chip text content', () => {
    const fixture = setup({ activeTypes: ['formatter'] });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('formatter');
  });

  it('should render no type chips when activeTypes is empty', () => {
    const fixture = setup({ activeTypes: [] });
    const chips = fixture.debugElement.queryAll(By.css('[data-testid="chip-type"]'));
    expect(chips).toHaveLength(0);
  });

  it('should render chips from all three categories simultaneously', () => {
    const fixture = setup({
      activeTypes: ['formatter'],
      activeLanguages: ['typescript'],
      activeUseCases: ['code-quality'],
    });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.textContent).toContain('formatter');
    expect(el.textContent).toContain('typescript');
    expect(el.textContent).toContain('code-quality');
  });

  it('should expose filtersChanged output', () => {
    const fixture = setup();
    expect(fixture.componentInstance.filtersChanged).toBeDefined();
  });
});
