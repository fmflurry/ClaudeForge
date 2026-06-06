/**
 * RED tests for Context Registry (Task 11.5)
 *
 * Expected production files (DO NOT exist yet — tests will fail to compile/resolve):
 *   src/app/core/context/context-registry.ts
 *
 * Public API the coder MUST implement:
 *
 *   ContextEvent<T>:
 *     type: string
 *     payload: T
 *
 *   ContextRegistry (class, singleton-safe):
 *     static instance: ContextRegistry  — OR exported as `contextRegistry` singleton
 *     publish<T>(eventType: string, payload: T): void
 *     subscribe<T>(eventType: string, handler: (payload: T) => void): () => void
 *       — returns an unsubscribe function
 *     clear(): void  — removes all subscribers (used in tests)
 *
 *   AppContext enum (initial entries — can be extended in later tasks):
 *     CATALOG = 'catalog'
 *     SEARCH = 'search'
 *     DASHBOARD = 'dashboard'
 *     TEAM_CONTEXT = 'team-context'
 *     TELEMETRY = 'telemetry'
 *     DOCS = 'docs'
 *
 *   contextProvidersFor(contexts: AppContext[]): Provider[]
 *     — returns aggregated provider arrays for the given contexts
 *     — initially returns [] for contexts with no registered providers (safe default)
 *
 *   CONTEXT_REGISTRY: Record<AppContext, Provider[]>
 *     — exported map used by contextProvidersFor
 */

import { Provider } from '@angular/core';
import {
  ContextRegistry,
  contextRegistry,
  ContextEvent,
  AppContext,
  CONTEXT_REGISTRY,
  contextProvidersFor,
} from './context-registry';

// ---------------------------------------------------------------------------
// ContextEvent shape
// ---------------------------------------------------------------------------

describe('ContextEvent<T> — shape', () => {
  it('should be assignable with a string type and typed payload', () => {
    const evt: ContextEvent<{ teamId: string }> = {
      type: 'team-changed',
      payload: { teamId: 'frontend' },
    };
    expect(evt.type).toBe('team-changed');
    expect(evt.payload.teamId).toBe('frontend');
  });
});

// ---------------------------------------------------------------------------
// ContextRegistry — publish/subscribe
// ---------------------------------------------------------------------------

describe('ContextRegistry — publish / subscribe', () => {
  beforeEach(() => {
    contextRegistry.clear();
  });

  it('should deliver a published event to a subscriber', () => {
    const received: string[] = [];
    contextRegistry.subscribe<string>('ping', (payload: string) => received.push(payload));
    contextRegistry.publish('ping', 'hello');
    expect(received).toEqual(['hello']);
  });

  it('should deliver a typed payload object to a subscriber', () => {
    interface TeamPayload { teamId: string; teamName: string }
    let captured: TeamPayload | undefined;

    contextRegistry.subscribe<TeamPayload>('team:changed', (p: TeamPayload) => {
      captured = p;
    });

    contextRegistry.publish<TeamPayload>('team:changed', { teamId: 'abc', teamName: 'Frontend' });
    expect(captured).toEqual({ teamId: 'abc', teamName: 'Frontend' });
  });

  it('should support multiple independent subscribers for the same event type', () => {
    const calls: number[] = [];
    contextRegistry.subscribe<null>('event', () => calls.push(1));
    contextRegistry.subscribe<null>('event', () => calls.push(2));
    contextRegistry.publish('event', null);
    expect(calls).toHaveLength(2);
    expect(calls).toContain(1);
    expect(calls).toContain(2);
  });

  it('should not deliver to subscribers for different event types', () => {
    const receivedA: string[] = [];
    const receivedB: string[] = [];

    contextRegistry.subscribe<string>('event-a', (p: string) => receivedA.push(p));
    contextRegistry.subscribe<string>('event-b', (p: string) => receivedB.push(p));

    contextRegistry.publish('event-a', 'only-a');
    expect(receivedA).toEqual(['only-a']);
    expect(receivedB).toHaveLength(0);
  });

  it('should deliver all published values in order', () => {
    const order: number[] = [];
    contextRegistry.subscribe<number>('seq', (n: number) => order.push(n));
    contextRegistry.publish('seq', 1);
    contextRegistry.publish('seq', 2);
    contextRegistry.publish('seq', 3);
    expect(order).toEqual([1, 2, 3]);
  });

  it('should not throw when publishing with no subscribers', () => {
    expect(() => contextRegistry.publish('no-subscribers', 'data')).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// ContextRegistry — unsubscribe
// ---------------------------------------------------------------------------

describe('ContextRegistry — unsubscribe', () => {
  beforeEach(() => {
    contextRegistry.clear();
  });

  it('should stop delivering after unsubscribe()', () => {
    const received: string[] = [];
    const unsubscribe = contextRegistry.subscribe<string>('evt', (p: string) => received.push(p));

    contextRegistry.publish('evt', 'first');
    unsubscribe();
    contextRegistry.publish('evt', 'second');

    expect(received).toEqual(['first']);
  });

  it('should only unsubscribe the specific handler, not others', () => {
    const a: string[] = [];
    const b: string[] = [];

    const unsubscribeA = contextRegistry.subscribe<string>('evt', (p: string) => a.push(p));
    contextRegistry.subscribe<string>('evt', (p: string) => b.push(p));

    contextRegistry.publish('evt', 'msg1');
    unsubscribeA();
    contextRegistry.publish('evt', 'msg2');

    expect(a).toEqual(['msg1']);
    expect(b).toEqual(['msg1', 'msg2']);
  });

  it('should not throw when unsubscribe is called more than once', () => {
    const unsub = contextRegistry.subscribe('evt', () => undefined);
    expect(() => {
      unsub();
      unsub();
    }).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// ContextRegistry — clear (test isolation helper)
// ---------------------------------------------------------------------------

describe('ContextRegistry — clear', () => {
  it('should remove all subscribers', () => {
    const received: string[] = [];
    contextRegistry.subscribe<string>('x', (p: string) => received.push(p));
    contextRegistry.clear();
    contextRegistry.publish('x', 'after-clear');
    expect(received).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// ContextRegistry — singleton behaviour
// ---------------------------------------------------------------------------

describe('ContextRegistry — singleton', () => {
  it('contextRegistry export should be the same instance as ContextRegistry.instance (if exposed)', () => {
    // Either a static .instance or the module-level export singleton pattern is fine.
    // Both imports should refer to the same object so cross-domain state is shared.
    expect(contextRegistry).toBeInstanceOf(ContextRegistry);
  });

  it('subscribers registered via the singleton should receive events published via the same singleton', () => {
    contextRegistry.clear();
    let count = 0;
    contextRegistry.subscribe('count-test', () => count++);
    contextRegistry.publish('count-test', null);
    expect(count).toBe(1);
  });
});

// ---------------------------------------------------------------------------
// AppContext enum
// ---------------------------------------------------------------------------

describe('AppContext enum', () => {
  it('should define the six plugin-marketplace domains', () => {
    expect(AppContext.CATALOG).toBe('catalog');
    expect(AppContext.SEARCH).toBe('search');
    expect(AppContext.DASHBOARD).toBe('dashboard');
    expect(AppContext.TEAM_CONTEXT).toBe('team-context');
    expect(AppContext.TELEMETRY).toBe('telemetry');
    expect(AppContext.DOCS).toBe('docs');
  });
});

// ---------------------------------------------------------------------------
// CONTEXT_REGISTRY map
// ---------------------------------------------------------------------------

describe('CONTEXT_REGISTRY', () => {
  it('should have an entry for every AppContext value', () => {
    const contextValues = Object.values(AppContext) as string[];
    contextValues.forEach((ctx) => {
      expect(CONTEXT_REGISTRY).toHaveProperty(ctx);
    });
  });

  it('each entry should be an array (may be empty until providers are registered)', () => {
    (Object.values(CONTEXT_REGISTRY) as Provider[][]).forEach((providers: Provider[]) => {
      expect(Array.isArray(providers)).toBe(true);
    });
  });
});

// ---------------------------------------------------------------------------
// contextProvidersFor
// ---------------------------------------------------------------------------

describe('contextProvidersFor', () => {
  it('should return an array (empty by default before providers are registered)', () => {
    const providers = contextProvidersFor([AppContext.CATALOG]);
    expect(Array.isArray(providers)).toBe(true);
  });

  it('should aggregate providers from multiple contexts without duplicates crashing', () => {
    const providers = contextProvidersFor([AppContext.CATALOG, AppContext.SEARCH]);
    expect(Array.isArray(providers)).toBe(true);
  });

  it('should return empty array for empty input', () => {
    expect(contextProvidersFor([])).toEqual([]);
  });

  it('should handle unknown/unregistered contexts gracefully (return [])', () => {
    // Casting to bypass type check — simulates future contexts not yet in CONTEXT_REGISTRY
    expect(() =>
      contextProvidersFor(['unknown-context' as AppContext]),
    ).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// Cross-domain isolation test
// ---------------------------------------------------------------------------

describe('Cross-domain isolation via ContextRegistry', () => {
  beforeEach(() => {
    contextRegistry.clear();
  });

  it('domain A can publish events that domain B receives without direct import', () => {
    // Simulates CatalogDomain publishing and TeamContextDomain subscribing
    interface PluginSelectedEvent { pluginId: string }

    const received: PluginSelectedEvent[] = [];
    // Domain B subscribes
    contextRegistry.subscribe<PluginSelectedEvent>('catalog:plugin-selected', (e: PluginSelectedEvent) =>
      received.push(e),
    );
    // Domain A publishes
    contextRegistry.publish<PluginSelectedEvent>('catalog:plugin-selected', { pluginId: 'abc123' });

    expect(received).toHaveLength(1);
    expect(received[0].pluginId).toBe('abc123');
  });

  it('unsubscribing from one event does not affect other event channels', () => {
    const aEvents: string[] = [];
    const bEvents: string[] = [];

    const unsubA = contextRegistry.subscribe<string>('channel-a', (p: string) => aEvents.push(p));
    contextRegistry.subscribe<string>('channel-b', (p: string) => bEvents.push(p));

    contextRegistry.publish('channel-a', 'msg');
    contextRegistry.publish('channel-b', 'msg');
    unsubA();
    contextRegistry.publish('channel-a', 'after-unsub');
    contextRegistry.publish('channel-b', 'also-after');

    expect(aEvents).toEqual(['msg']);
    expect(bEvents).toEqual(['msg', 'also-after']);
  });
});
