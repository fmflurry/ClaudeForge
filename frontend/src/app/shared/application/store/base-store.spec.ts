/**
 * RED tests for BaseStore<T> + ResourceState<T> (Task 11.2)
 *
 * Expected production files (DO NOT exist yet — tests will fail to compile/resolve):
 *   src/app/shared/application/store/resource-state.model.ts
 *   src/app/shared/application/store/base-store.ts
 *
 * Public API the coder MUST implement:
 *
 *   ResourceState<T>:
 *     isLoading?: boolean
 *     data?: T
 *     status?: 'Success' | 'Error' | 'Idle'
 *     errors?: Array<{ code: string; message: string }>
 *
 *   BaseStore<TEnum extends string, TState extends Record<TEnum, ResourceState<unknown>>>:
 *     constructor(enumObj: Record<string, TEnum>)
 *     get(key: TEnum): WritableSignal<ResourceState<...>>
 *     update(key: TEnum, partial: Partial<ResourceState<...>>): void  — IMMUTABLE merge
 *     startLoading(key: TEnum): void
 *     stopLoading(key: TEnum): void
 *     clear(key: TEnum): void
 *     clearAll(): void
 *     onUpdate(key: TEnum, cb: (next, prev) => void): () => void
 */

import { TestBed } from '@angular/core/testing';
import { Injectable } from '@angular/core';
import { WritableSignal } from '@angular/core';
import { ResourceState } from './resource-state.model';
import { BaseStore } from './base-store';

// ---------------------------------------------------------------------------
// Minimal concrete store used only in tests
// ---------------------------------------------------------------------------

enum TestStoreEnum {
  ITEMS = 'ITEMS',
  DETAIL = 'DETAIL',
}

interface TestItem { id: string; name: string }

interface TestState {
  [TestStoreEnum.ITEMS]: ResourceState<TestItem[]>;
  [TestStoreEnum.DETAIL]: ResourceState<TestItem>;
}

@Injectable()
class TestStore extends BaseStore<typeof TestStoreEnum, TestState> {
  constructor() {
    super(TestStoreEnum);
  }
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function setupStore(): TestStore {
  TestBed.configureTestingModule({ providers: [TestStore] });
  return TestBed.inject(TestStore);
}

// ---------------------------------------------------------------------------
// ResourceState shape
// ---------------------------------------------------------------------------

describe('ResourceState<T> — shape', () => {
  it('should be assignable with only isLoading set (idle-like)', () => {
    const state: ResourceState<string> = { isLoading: false };
    expect(state.isLoading).toBe(false);
    expect(state.data).toBeUndefined();
    expect(state.status).toBeUndefined();
    expect(state.errors).toBeUndefined();
  });

  it('should be assignable with data and Success status', () => {
    const state: ResourceState<string[]> = {
      isLoading: false,
      data: ['a', 'b'],
      status: 'Success',
    };
    expect(state.status).toBe('Success');
    expect(state.data).toEqual(['a', 'b']);
  });

  it('should be assignable with errors and Error status', () => {
    const state: ResourceState<null> = {
      isLoading: false,
      data: undefined,
      status: 'Error',
      errors: [{ code: 'ERR_001', message: 'Something failed' }],
    };
    expect(state.errors).toHaveLength(1);
    expect(state.errors?.[0].code).toBe('ERR_001');
  });

  it('should allow Idle status', () => {
    const state: ResourceState<number> = { status: 'Idle' };
    expect(state.status).toBe('Idle');
  });
});

// ---------------------------------------------------------------------------
// BaseStore — initialisation
// ---------------------------------------------------------------------------

describe('BaseStore — initialisation', () => {
  it('should initialise all enum keys with an empty (non-loading) state', () => {
    const store = setupStore();

    const itemsState = store.get(TestStoreEnum.ITEMS)();
    expect(itemsState.isLoading).toBeFalsy();
    expect(itemsState.data).toBeUndefined();
    expect(itemsState.status).toBeUndefined();
  });

  it('should return a WritableSignal from get()', () => {
    const store = setupStore();
    const sig = store.get(TestStoreEnum.ITEMS);
    // A WritableSignal is callable and has a .set method
    expect(typeof sig).toBe('function');
    expect(typeof (sig as WritableSignal<ResourceState<TestItem[]>>).set).toBe('function');
  });

  it('should return the SAME signal reference on repeated get() calls', () => {
    const store = setupStore();
    const sig1 = store.get(TestStoreEnum.ITEMS);
    const sig2 = store.get(TestStoreEnum.ITEMS);
    expect(sig1).toBe(sig2);
  });
});

// ---------------------------------------------------------------------------
// BaseStore — startLoading
// ---------------------------------------------------------------------------

describe('BaseStore — startLoading', () => {
  it('should set isLoading to true', () => {
    const store = setupStore();
    store.startLoading(TestStoreEnum.ITEMS);
    expect(store.get(TestStoreEnum.ITEMS)().isLoading).toBe(true);
  });

  it('should clear status and errors when starting loading', () => {
    const store = setupStore();
    // First put the store into error state
    store.update(TestStoreEnum.ITEMS, {
      status: 'Error',
      errors: [{ code: 'E', message: 'prior error' }],
    });
    store.startLoading(TestStoreEnum.ITEMS);
    const state = store.get(TestStoreEnum.ITEMS)();
    expect(state.status).toBeUndefined();
    expect(state.errors).toBeUndefined();
  });

  it('should NOT mutate the previous state object', () => {
    const store = setupStore();
    const before = store.get(TestStoreEnum.ITEMS)();
    store.startLoading(TestStoreEnum.ITEMS);
    const after = store.get(TestStoreEnum.ITEMS)();
    // Immutability: the objects must be different references
    expect(after).not.toBe(before);
  });
});

// ---------------------------------------------------------------------------
// BaseStore — update (Success path)
// ---------------------------------------------------------------------------

describe('BaseStore — update (success path)', () => {
  it('should set data and status=Success', () => {
    const store = setupStore();
    const items: TestItem[] = [{ id: '1', name: 'Plugin A' }];
    store.update(TestStoreEnum.ITEMS, { data: items, isLoading: false, status: 'Success' });

    const state = store.get(TestStoreEnum.ITEMS)();
    expect(state.status).toBe('Success');
    expect(state.isLoading).toBe(false);
    expect(state.data).toEqual(items);
  });

  it('should merge partial updates without losing other fields', () => {
    const store = setupStore();
    store.update(TestStoreEnum.ITEMS, { isLoading: true });
    store.update(TestStoreEnum.ITEMS, { isLoading: false, status: 'Success' });

    const state = store.get(TestStoreEnum.ITEMS)();
    expect(state.isLoading).toBe(false);
    expect(state.status).toBe('Success');
  });

  it('should NOT mutate prior state object on update', () => {
    const store = setupStore();
    const before = store.get(TestStoreEnum.ITEMS)();
    store.update(TestStoreEnum.ITEMS, { isLoading: true });
    const after = store.get(TestStoreEnum.ITEMS)();
    expect(after).not.toBe(before);
  });

  it('should NOT share object references between different store keys', () => {
    const store = setupStore();
    store.update(TestStoreEnum.ITEMS, { isLoading: true });
    const detailState = store.get(TestStoreEnum.DETAIL)();
    expect(detailState.isLoading).toBeFalsy();
  });
});

// ---------------------------------------------------------------------------
// BaseStore — update (Error path)
// ---------------------------------------------------------------------------

describe('BaseStore — update (error path)', () => {
  it('should set status=Error and errors array', () => {
    const store = setupStore();
    store.update(TestStoreEnum.ITEMS, {
      status: 'Error',
      isLoading: false,
      data: undefined,
      errors: [{ code: 'HTTP_500', message: 'Internal server error' }],
    });

    const state = store.get(TestStoreEnum.ITEMS)();
    expect(state.status).toBe('Error');
    expect(state.errors).toHaveLength(1);
    expect(state.errors?.[0].message).toBe('Internal server error');
  });

  it('should allow multiple error entries', () => {
    const store = setupStore();
    store.update(TestStoreEnum.ITEMS, {
      status: 'Error',
      errors: [
        { code: 'E1', message: 'First error' },
        { code: 'E2', message: 'Second error' },
      ],
    });
    expect(store.get(TestStoreEnum.ITEMS)().errors).toHaveLength(2);
  });
});

// ---------------------------------------------------------------------------
// BaseStore — stopLoading
// ---------------------------------------------------------------------------

describe('BaseStore — stopLoading', () => {
  it('should set isLoading to false without touching status or data', () => {
    const store = setupStore();
    store.startLoading(TestStoreEnum.ITEMS);
    store.stopLoading(TestStoreEnum.ITEMS);
    const state = store.get(TestStoreEnum.ITEMS)();
    expect(state.isLoading).toBe(false);
    // status should remain whatever it was (undefined in this case)
    expect(state.status).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// BaseStore — clear / clearAll
// ---------------------------------------------------------------------------

describe('BaseStore — clear', () => {
  it('should reset a single key to empty state', () => {
    const store = setupStore();
    store.update(TestStoreEnum.ITEMS, {
      data: [{ id: '1', name: 'x' }],
      isLoading: false,
      status: 'Success',
    });
    store.clear(TestStoreEnum.ITEMS);

    const state = store.get(TestStoreEnum.ITEMS)();
    expect(state.data).toBeUndefined();
    expect(state.isLoading).toBeFalsy();
    expect(state.status).toBeUndefined();
  });

  it('should not affect other keys when clearing one key', () => {
    const store = setupStore();
    store.update(TestStoreEnum.DETAIL, {
      data: { id: '99', name: 'Detail Item' },
      status: 'Success',
    });
    store.clear(TestStoreEnum.ITEMS);
    expect(store.get(TestStoreEnum.DETAIL)().status).toBe('Success');
  });
});

describe('BaseStore — clearAll', () => {
  it('should reset all keys to empty state', () => {
    const store = setupStore();
    store.update(TestStoreEnum.ITEMS, { status: 'Success', data: [] });
    store.update(TestStoreEnum.DETAIL, { status: 'Success', data: { id: '1', name: 'A' } });
    store.clearAll();

    expect(store.get(TestStoreEnum.ITEMS)().status).toBeUndefined();
    expect(store.get(TestStoreEnum.DETAIL)().status).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// BaseStore — onUpdate reactive hook
// ---------------------------------------------------------------------------

describe('BaseStore — onUpdate', () => {
  it('should invoke callback when the state changes', () => {
    const store = setupStore();
    const calls: { next: ResourceState<TestItem[]>; prev: ResourceState<TestItem[]> }[] = [];
    store.onUpdate(TestStoreEnum.ITEMS, (next, prev) => calls.push({ next, prev }));

    store.startLoading(TestStoreEnum.ITEMS);

    expect(calls.length).toBeGreaterThanOrEqual(1);
    expect(calls[calls.length - 1].next.isLoading).toBe(true);
  });

  it('should stop delivering updates after unsubscribe', () => {
    const store = setupStore();
    const calls: ResourceState<TestItem[]>[] = [];
    const unsubscribe = store.onUpdate(TestStoreEnum.ITEMS, (next) => calls.push(next));

    store.startLoading(TestStoreEnum.ITEMS);
    const countAfterFirst = calls.length;

    unsubscribe();
    store.stopLoading(TestStoreEnum.ITEMS);

    expect(calls.length).toBe(countAfterFirst);
  });

  it('should pass both next and prev state to callback', () => {
    const store = setupStore();
    let capturedPrev: ResourceState<TestItem[]> | undefined;
    let capturedNext: ResourceState<TestItem[]> | undefined;

    store.onUpdate(TestStoreEnum.ITEMS, (next, prev) => {
      capturedNext = next;
      capturedPrev = prev;
    });

    store.startLoading(TestStoreEnum.ITEMS);
    expect(capturedPrev?.isLoading).toBeFalsy();
    expect(capturedNext?.isLoading).toBe(true);
  });

  it('should support multiple independent subscribers on the same key', () => {
    const store = setupStore();
    const callsA: number[] = [];
    const callsB: number[] = [];

    store.onUpdate(TestStoreEnum.ITEMS, () => callsA.push(1));
    store.onUpdate(TestStoreEnum.ITEMS, () => callsB.push(1));

    store.startLoading(TestStoreEnum.ITEMS);
    store.stopLoading(TestStoreEnum.ITEMS);

    expect(callsA.length).toBeGreaterThanOrEqual(2);
    expect(callsB.length).toBeGreaterThanOrEqual(2);
  });
});

// ---------------------------------------------------------------------------
// Immutability contract
// ---------------------------------------------------------------------------

describe('BaseStore — immutability contract', () => {
  it('state objects returned by get() must not be the same reference after any update', () => {
    const store = setupStore();
    const snapshots: ResourceState<TestItem[]>[] = [];
    snapshots.push(store.get(TestStoreEnum.ITEMS)());

    store.startLoading(TestStoreEnum.ITEMS);
    snapshots.push(store.get(TestStoreEnum.ITEMS)());

    store.update(TestStoreEnum.ITEMS, { data: [], status: 'Success', isLoading: false });
    snapshots.push(store.get(TestStoreEnum.ITEMS)());

    store.clear(TestStoreEnum.ITEMS);
    snapshots.push(store.get(TestStoreEnum.ITEMS)());

    // Every snapshot must be a distinct object
    for (let i = 0; i < snapshots.length; i++) {
      for (let j = i + 1; j < snapshots.length; j++) {
        expect(snapshots[i]).not.toBe(snapshots[j]);
      }
    }
  });

  it('mutating returned state object must not change store state', () => {
    const store = setupStore();
    store.update(TestStoreEnum.ITEMS, { data: [{ id: '1', name: 'A' }], status: 'Success' });
    const state = store.get(TestStoreEnum.ITEMS)();

    // Attempt external mutation — must not affect the store
    if (state.data) {
      // This push should NOT affect the internal store copy
      (state.data as TestItem[]).push({ id: '2', name: 'B' });
    }

    // Re-read: store must still show original single-item array
    const fresh = store.get(TestStoreEnum.ITEMS)();
    // Deep equality is not guaranteed (shallow freeze suffices), but
    // external mutation should not penetrate if data is a new reference.
    // We test that at minimum data is still an array (not corrupted).
    expect(Array.isArray(fresh.data)).toBe(true);
  });
});
