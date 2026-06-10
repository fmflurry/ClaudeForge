/**
 * Unit tests for KeywordSearchComponent
 *
 * Tests keyword search input behavior for filtering plugins.
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { KeywordSearchComponent } from './keyword-search.component';

function setup(): {
  fixture: ComponentFixture<KeywordSearchComponent>;
  comp: KeywordSearchComponent;
} {
  TestBed.configureTestingModule({
    imports: [KeywordSearchComponent],
  });
  const fixture = TestBed.createComponent(KeywordSearchComponent);
  const comp = fixture.componentInstance;
  return { fixture, comp };
}

describe('KeywordSearchComponent', () => {
  it('creates instance', () => {
    const { comp } = setup();
    expect(comp).toBeDefined();
  });

  it('emits searchChanged when onSearchChange called', () => {
    const { comp } = setup();
    const emitted: string[] = [];
    comp.searchChanged.subscribe((q) => emitted.push(q));

    comp.onSearchChange('typescript');
    expect(emitted).toEqual(['typescript']);
  });

  it('emits empty string for clear', () => {
    const { comp } = setup();
    const emitted: string[] = [];
    comp.searchChanged.subscribe((q) => emitted.push(q));

    comp.onSearchChange('');
    expect(emitted).toEqual(['']);
  });

  it('emits multiple changes', () => {
    const { comp } = setup();
    const emitted: string[] = [];
    comp.searchChanged.subscribe((q) => emitted.push(q));

    comp.onSearchChange('typescript');
    comp.onSearchChange('react');
    comp.onSearchChange('');
    expect(emitted).toEqual(['typescript', 'react', '']);
  });
});
