/**
 * SearchBarComponent — render + action wiring tests.
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChangeDetectionStrategy } from '@angular/core';
import { By } from '@angular/platform-browser';
import { SearchBarComponent } from './search-bar.component';

// ---------------------------------------------------------------------------
// Setup
// ---------------------------------------------------------------------------

function setup(
  opts: {
    initialKeyword?: string;
    isLoading?: boolean;
  } = {},
): ComponentFixture<SearchBarComponent> {
  TestBed.resetTestingModule();
  TestBed.configureTestingModule({
    imports: [SearchBarComponent],
  }).overrideComponent(SearchBarComponent, {
    set: { changeDetection: ChangeDetectionStrategy.Default },
  });
  const fixture = TestBed.createComponent(SearchBarComponent);
  if (opts.initialKeyword !== undefined) {
    fixture.componentRef.setInput('initialKeyword', opts.initialKeyword);
  }
  if (opts.isLoading !== undefined) {
    fixture.componentRef.setInput('isLoading', opts.isLoading);
  }
  fixture.detectChanges();
  return fixture;
}

// ---------------------------------------------------------------------------
// Render tests
// ---------------------------------------------------------------------------

describe('SearchBarComponent — render', () => {
  it('should instantiate', () => {
    const fixture = setup();
    expect(fixture.componentInstance).toBeDefined();
  });

  it('should render a search input', () => {
    const fixture = setup();
    const input = fixture.debugElement.query(By.css('input[type="search"]'));
    expect(input).not.toBeNull();
  });

  it('should render the Search button', () => {
    const fixture = setup();
    const btn = fixture.debugElement.query(By.css('button[data-testid="search-button"]'));
    expect(btn).not.toBeNull();
  });

  it('should populate input value from initialKeyword input', () => {
    const fixture = setup({ initialKeyword: 'typescript' });
    const input = fixture.debugElement.query(By.css('input[type="search"]'));
    expect((input.nativeElement as HTMLInputElement).value).toBe('typescript');
  });

  it('should set aria-busy on input when isLoading is true', () => {
    const fixture = setup({ isLoading: true });
    const input = fixture.debugElement.query(By.css('input[type="search"]'));
    expect((input.nativeElement as HTMLInputElement).getAttribute('aria-busy')).toBe('true');
  });

  it('should NOT set aria-busy when isLoading is false', () => {
    const fixture = setup({ isLoading: false });
    const input = fixture.debugElement.query(By.css('input[type="search"]'));
    const ariaBusy = (input.nativeElement as HTMLInputElement).getAttribute('aria-busy');
    // aria-busy should be null or absent when false
    expect(ariaBusy).toBeNull();
  });
});

// ---------------------------------------------------------------------------
// onSearch
// ---------------------------------------------------------------------------

describe('SearchBarComponent — onSearch', () => {
  it('should emit searchSubmitted with trimmed keyword', () => {
    const fixture = setup();
    const emitted: string[] = [];
    fixture.componentInstance.searchSubmitted.subscribe((kw: string) => emitted.push(kw));
    fixture.componentInstance.onSearch('  typescript  ');
    expect(emitted[0]).toBe('typescript');
  });

  it('should emit searchSubmitted with empty string when keyword is only spaces', () => {
    const fixture = setup();
    const emitted: string[] = [];
    fixture.componentInstance.searchSubmitted.subscribe((kw: string) => emitted.push(kw));
    fixture.componentInstance.onSearch('   ');
    expect(emitted[0]).toBe('');
  });

  it('should emit searchSubmitted when Search button is clicked', () => {
    const fixture = setup({ initialKeyword: 'plugin' });
    const emitted: string[] = [];
    fixture.componentInstance.searchSubmitted.subscribe((kw: string) => emitted.push(kw));
    const btn = fixture.debugElement.query(By.css('button[data-testid="search-button"]'));
    btn.triggerEventHandler('click', null);
    expect(emitted).toHaveLength(1);
  });

  it('should not throw when onSearch is called', () => {
    const fixture = setup();
    expect(() => fixture.componentInstance.onSearch('test')).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// Inputs / Outputs contract
// ---------------------------------------------------------------------------

describe('SearchBarComponent — inputs / outputs', () => {
  it('should expose searchSubmitted output', () => {
    const fixture = setup();
    expect(fixture.componentInstance.searchSubmitted).toBeDefined();
  });

  it('should expose filtersChanged output', () => {
    const fixture = setup();
    expect(fixture.componentInstance.filtersChanged).toBeDefined();
  });

  it('should expose initialKeyword input', () => {
    const fixture = setup();
    expect('initialKeyword' in fixture.componentInstance).toBe(true);
  });

  it('should expose isLoading input', () => {
    const fixture = setup();
    expect('isLoading' in fixture.componentInstance).toBe(true);
  });
});
