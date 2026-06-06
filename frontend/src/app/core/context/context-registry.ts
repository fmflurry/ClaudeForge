import { Provider } from '@angular/core';

// ---------------------------------------------------------------------------
// ContextEvent<T>
// ---------------------------------------------------------------------------

export interface ContextEvent<T> {
  type: string;
  payload: T;
}

// ---------------------------------------------------------------------------
// ContextRegistry
// ---------------------------------------------------------------------------

interface HandlerEntry {
  id: number;
  fn: (payload: unknown) => void;
}

export class ContextRegistry {
  private readonly subscribers = new Map<string, HandlerEntry[]>();
  private nextId = 0;

  publish<T>(eventType: string, payload: T): void {
    const list = this.subscribers.get(eventType);
    if (!list) return;
    // Iterate over a shallow copy so mid-iteration unsubscriptions are safe.
    for (const entry of [...list]) {
      entry.fn(payload as unknown);
    }
  }

  subscribe<T>(eventType: string, handler: (payload: T) => void): () => void {
    if (!this.subscribers.has(eventType)) {
      this.subscribers.set(eventType, []);
    }

    const id = this.nextId++;
    const entry: HandlerEntry = { id, fn: handler as (payload: unknown) => void };
    this.subscribers.get(eventType)!.push(entry);

    let unsubscribed = false;
    return () => {
      if (unsubscribed) return;
      unsubscribed = true;
      const current = this.subscribers.get(eventType);
      if (!current) return;
      const idx = current.findIndex((e) => e.id === id);
      if (idx !== -1) {
        current.splice(idx, 1);
      }
    };
  }

  /** Removes all subscribers — used in tests for isolation. */
  clear(): void {
    this.subscribers.clear();
  }
}

// ---------------------------------------------------------------------------
// Module-level singleton
// ---------------------------------------------------------------------------

export const contextRegistry = new ContextRegistry();

// ---------------------------------------------------------------------------
// AppContext enum
// ---------------------------------------------------------------------------

export enum AppContext {
  CATALOG = 'catalog',
  SEARCH = 'search',
  DASHBOARD = 'dashboard',
  TEAM_CONTEXT = 'team-context',
  TELEMETRY = 'telemetry',
  DOCS = 'docs',
}

// ---------------------------------------------------------------------------
// CONTEXT_REGISTRY — maps each AppContext to its Angular providers
// ---------------------------------------------------------------------------

export const CONTEXT_REGISTRY: Record<AppContext, Provider[]> = {
  [AppContext.CATALOG]: [],
  [AppContext.SEARCH]: [],
  [AppContext.DASHBOARD]: [],
  [AppContext.TEAM_CONTEXT]: [],
  [AppContext.TELEMETRY]: [],
  [AppContext.DOCS]: [],
};

// ---------------------------------------------------------------------------
// contextProvidersFor — aggregates providers for the requested contexts
// ---------------------------------------------------------------------------

export function contextProvidersFor(contexts: AppContext[]): Provider[] {
  return contexts.flatMap((ctx) => {
    const providers = CONTEXT_REGISTRY[ctx];
    return providers ?? [];
  });
}
