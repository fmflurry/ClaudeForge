/**
 * Unit tests for StructuralTypeCheckboxesComponent
 *
 * Tests multi-select checkbox behavior for structural type filtering.
 */

import { ComponentFixture, TestBed } from '@angular/core/testing';
import { StructuralTypeCheckboxesComponent } from './structural-type-checkboxes.component';
import type { StructuralType } from './structural-type-checkboxes.component';

function makeTypes(): readonly StructuralType[] {
  return [
    { id: 'skill', label: 'Skill', count: 5 },
    { id: 'hook', label: 'Hook', count: 3 },
    { id: 'subagent', label: 'Subagent', count: 2 },
  ];
}

function setup(selectedTypes: readonly string[] = []): {
  fixture: ComponentFixture<StructuralTypeCheckboxesComponent>;
  comp: StructuralTypeCheckboxesComponent;
} {
  TestBed.configureTestingModule({
    imports: [StructuralTypeCheckboxesComponent],
  });
  const fixture = TestBed.createComponent(StructuralTypeCheckboxesComponent);
  fixture.componentRef.setInput('types', makeTypes());
  fixture.componentRef.setInput('selectedTypes', selectedTypes);
  const comp = fixture.componentInstance;
  return { fixture, comp };
}

describe('StructuralTypeCheckboxesComponent', () => {
  it('creates instance', () => {
    const { comp } = setup();
    expect(comp).toBeDefined();
  });

  describe('isSelected', () => {
    it('returns false when no types selected', () => {
      const { comp } = setup([]);
      expect(comp.isSelected('skill')).toBe(false);
    });

    it('returns true when type is in selectedTypes', () => {
      const { comp } = setup(['skill', 'hook']);
      expect(comp.isSelected('skill')).toBe(true);
      expect(comp.isSelected('hook')).toBe(true);
    });

    it('returns false when type is not in selectedTypes', () => {
      const { comp } = setup(['skill']);
      expect(comp.isSelected('hook')).toBe(false);
    });
  });

  describe('onTypeToggle', () => {
    it('adds type when not currently selected', () => {
      const { comp, fixture } = setup([]);
      const emitted: readonly string[][] = [];
      comp.typesChanged.subscribe((types) => (emitted as string[][]).push([...types]));

      comp.onTypeToggle('skill');
      expect(emitted).toEqual([['skill']]);

      // Simulate parent sync: update input with emitted value
      fixture.componentRef.setInput('selectedTypes', emitted[emitted.length - 1]);
    });

    it('adds type to existing selection', () => {
      const { comp, fixture } = setup(['skill']);
      const emitted: readonly string[][] = [];
      comp.typesChanged.subscribe((types) => (emitted as string[][]).push([...types]));

      comp.onTypeToggle('hook');
      expect(emitted).toEqual([['skill', 'hook']]);

      fixture.componentRef.setInput('selectedTypes', emitted[emitted.length - 1]);
    });

    it('removes type when currently selected', () => {
      const { comp, fixture } = setup(['skill', 'hook']);
      const emitted: readonly string[][] = [];
      comp.typesChanged.subscribe((types) => (emitted as string[][]).push([...types]));

      comp.onTypeToggle('skill');
      expect(emitted).toEqual([['hook']]);

      fixture.componentRef.setInput('selectedTypes', emitted[emitted.length - 1]);
    });

    it('removes last type resulting in empty array', () => {
      const { comp, fixture } = setup(['skill']);
      const emitted: readonly string[][] = [];
      comp.typesChanged.subscribe((types) => (emitted as string[][]).push([...types]));

      comp.onTypeToggle('skill');
      expect(emitted).toEqual([[]]);

      fixture.componentRef.setInput('selectedTypes', emitted[emitted.length - 1]);
    });

    it('toggles multiple types independently', () => {
      const { comp, fixture } = setup([]);
      const emitted: readonly string[][] = [];
      comp.typesChanged.subscribe((types) => (emitted as string[][]).push([...types]));

      comp.onTypeToggle('skill');
      fixture.componentRef.setInput('selectedTypes', ['skill']);

      comp.onTypeToggle('hook');
      fixture.componentRef.setInput('selectedTypes', ['skill', 'hook']);

      comp.onTypeToggle('skill'); // remove skill
      fixture.componentRef.setInput('selectedTypes', ['hook']);

      expect(emitted).toEqual([['skill'], ['skill', 'hook'], ['hook']]);
    });
  });
});
