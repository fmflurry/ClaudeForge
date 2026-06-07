import { signal, WritableSignal } from '@angular/core';
import { ResourceState } from './resource-state.model';

type Listener<T> = (next: ResourceState<T>, prev: ResourceState<T>) => void;

interface ListenerEntry {
  id: number;
  fn: Listener<unknown>;
}

/**
 * Abstract signal-based store.
 *
 * TEnum  — the enum object type (e.g. typeof MyStoreEnum)
 * TState — a Record mapping enum string values → ResourceState<T>
 */
export abstract class BaseStore<
  TEnum extends Record<string, string>,
  TState extends Record<TEnum[keyof TEnum], ResourceState<unknown>>,
> {
  /** One signal per enum key. */
  private readonly signals = new Map<string, WritableSignal<ResourceState<unknown>>>();

  /** Per-key listener lists. */
  private readonly listeners = new Map<string, ListenerEntry[]>();

  private nextListenerId = 0;

  constructor(private readonly enumObj: TEnum) {
    // Pre-initialise all signals so every key starts with an empty state object.
    for (const key of Object.values(enumObj)) {
      const k = key as string;
      this.signals.set(k, signal<ResourceState<unknown>>({}));
      this.listeners.set(k, []);
    }
  }

  // ---------------------------------------------------------------------------
  // Public read API
  // ---------------------------------------------------------------------------

  /**
   * Returns the WritableSignal for a given key.
   * Always returns the SAME signal reference for the same key.
   */
  get<K extends TEnum[keyof TEnum]>(key: K): WritableSignal<TState[K]> {
    const sig = this.requireSignal(key);
    return sig as WritableSignal<TState[K]>;
  }

  // ---------------------------------------------------------------------------
  // Public write API
  // ---------------------------------------------------------------------------

  /** Immutable merge — spreads the partial onto the previous state. */
  update<K extends TEnum[keyof TEnum]>(key: K, partial: Partial<TState[K]>): void {
    const sig = this.requireSignal(key);
    const prev = sig();
    const next: ResourceState<unknown> = { ...prev, ...partial };
    sig.set(next);
    this.notify(key, next, prev);
  }

  /** Sets isLoading=true, clears status and errors. */
  startLoading<K extends TEnum[keyof TEnum]>(key: K): void {
    const sig = this.requireSignal(key);
    const prev = sig();
    const next: ResourceState<unknown> = {
      ...prev,
      isLoading: true,
      status: undefined,
      errors: undefined,
    };
    sig.set(next);
    this.notify(key, next, prev);
  }

  /** Sets isLoading=false without touching status or data. */
  stopLoading<K extends TEnum[keyof TEnum]>(key: K): void {
    const sig = this.requireSignal(key);
    const prev = sig();
    const next: ResourceState<unknown> = { ...prev, isLoading: false };
    sig.set(next);
    this.notify(key, next, prev);
  }

  /** Resets a single key to empty (non-loading) state. */
  clear<K extends TEnum[keyof TEnum]>(key: K): void {
    const sig = this.requireSignal(key);
    const prev = sig();
    const next: ResourceState<unknown> = {};
    sig.set(next);
    this.notify(key, next, prev);
  }

  /** Resets all keys to empty state. */
  clearAll(): void {
    for (const key of Object.values(this.enumObj)) {
      const k = key as string;
      const sig = this.requireSignal(k);
      const prev = sig();
      const next: ResourceState<unknown> = {};
      sig.set(next);
      this.notify(k, next, prev);
    }
  }

  // ---------------------------------------------------------------------------
  // Reactive hook
  // ---------------------------------------------------------------------------

  /**
   * Registers a callback invoked whenever the state for `key` changes.
   * Returns an idempotent unsubscribe function.
   */
  onUpdate<K extends TEnum[keyof TEnum]>(key: K, cb: (next: TState[K], prev: TState[K]) => void): () => void {
    const id = this.nextListenerId++;
    const list = this.requireListenerList(key);

    const entry: ListenerEntry = {
      id,
      fn: cb as Listener<unknown>,
    };
    list.push(entry);

    let unsubscribed = false;
    return () => {
      if (unsubscribed) return;
      unsubscribed = true;
      const current = this.requireListenerList(key);
      const idx = current.findIndex((e) => e.id === id);
      if (idx !== -1) {
        current.splice(idx, 1);
      }
    };
  }

  // ---------------------------------------------------------------------------
  // Private helpers
  // ---------------------------------------------------------------------------

  private requireSignal(key: string): WritableSignal<ResourceState<unknown>> {
    const sig = this.signals.get(key);
    if (!sig) {
      throw new Error(`BaseStore: unknown key "${key}"`);
    }
    return sig;
  }

  private requireListenerList(key: string): ListenerEntry[] {
    const list = this.listeners.get(key);
    if (!list) {
      throw new Error(`BaseStore: unknown key "${key}"`);
    }
    return list;
  }

  private notify(key: string, next: ResourceState<unknown>, prev: ResourceState<unknown>): void {
    const list = this.listeners.get(key);
    if (!list) return;
    // Iterate over a shallow copy so mid-iteration unsubscriptions are safe.
    for (const entry of [...list]) {
      entry.fn(next, prev);
    }
  }
}
