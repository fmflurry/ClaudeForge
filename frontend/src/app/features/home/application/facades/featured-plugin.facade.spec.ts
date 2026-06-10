/**
 * Tests for FeaturedPluginFacade.
 * Follows the pattern of home-metrics.facade.spec.ts.
 */

import { TestBed } from '@angular/core/testing';
import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { FeaturedPluginFacade } from './featured-plugin.facade';
import { FeaturedPluginPort } from '../../domain/ports/featured-plugin.port';
import type { FeaturedPlugin } from '../../domain/models/featured-plugin.model';

// ---------------------------------------------------------------------------
// Fake port implementations
// ---------------------------------------------------------------------------

const FAKE_PLUGIN: FeaturedPlugin = {
  pluginId: 'plugin-abc',
  name: 'Awesome Plugin',
  slug: 'awesome-plugin',
  latestVersion: '1.2.3',
};

@Injectable()
class FakeFeaturedPluginPort extends FeaturedPluginPort {
  override getFeaturedPlugin(): Observable<FeaturedPlugin | null> {
    return of(FAKE_PLUGIN);
  }
}

@Injectable()
class NullFeaturedPluginPort extends FeaturedPluginPort {
  override getFeaturedPlugin(): Observable<FeaturedPlugin | null> {
    return of(null);
  }
}

// ---------------------------------------------------------------------------
// Setup helpers
// ---------------------------------------------------------------------------

function setupWithPlugin(): { facade: FeaturedPluginFacade } {
  TestBed.configureTestingModule({
    providers: [FeaturedPluginFacade, { provide: FeaturedPluginPort, useClass: FakeFeaturedPluginPort }],
  });
  return { facade: TestBed.inject(FeaturedPluginFacade) };
}

function setupWithNull(): { facade: FeaturedPluginFacade } {
  TestBed.configureTestingModule({
    providers: [FeaturedPluginFacade, { provide: FeaturedPluginPort, useClass: NullFeaturedPluginPort }],
  });
  return { facade: TestBed.inject(FeaturedPluginFacade) };
}

// ---------------------------------------------------------------------------
// Initial state
// ---------------------------------------------------------------------------

describe('FeaturedPluginFacade — initial state', () => {
  it('featuredPlugin() should be null before load()', () => {
    const { facade } = setupWithPlugin();
    expect(facade.featuredPlugin()).toBeNull();
  });

  it('isLoading() should be false before load()', () => {
    const { facade } = setupWithPlugin();
    expect(facade.isLoading()).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// Signal API surface
// ---------------------------------------------------------------------------

describe('FeaturedPluginFacade — signal API surface', () => {
  it('featuredPlugin should be a callable signal function', () => {
    const { facade } = setupWithPlugin();
    expect(typeof facade.featuredPlugin).toBe('function');
  });

  it('isLoading should be a callable signal function', () => {
    const { facade } = setupWithPlugin();
    expect(typeof facade.isLoading).toBe('function');
  });

  it('load should be a method', () => {
    const { facade } = setupWithPlugin();
    expect(typeof facade.load).toBe('function');
  });
});

// ---------------------------------------------------------------------------
// load() — success (featured plugin present)
// ---------------------------------------------------------------------------

describe('FeaturedPluginFacade — load() with plugin', () => {
  it('should set featuredPlugin() after successful load', () => {
    const { facade } = setupWithPlugin();
    facade.load();
    expect(facade.featuredPlugin()).toEqual(FAKE_PLUGIN);
  });

  it('should set isLoading to false after load', () => {
    const { facade } = setupWithPlugin();
    facade.load();
    expect(facade.isLoading()).toBe(false);
  });

  it('should expose the slug from the featured plugin', () => {
    const { facade } = setupWithPlugin();
    facade.load();
    expect(facade.featuredPlugin()?.slug).toBe('awesome-plugin');
  });

  it('should expose the latestVersion from the featured plugin', () => {
    const { facade } = setupWithPlugin();
    facade.load();
    expect(facade.featuredPlugin()?.latestVersion).toBe('1.2.3');
  });
});

// ---------------------------------------------------------------------------
// load() — null result (none featured / fetch failed → adapter returns null)
// ---------------------------------------------------------------------------

describe('FeaturedPluginFacade — load() with null (no featured plugin)', () => {
  it('should set featuredPlugin() to null when port returns null', () => {
    const { facade } = setupWithNull();
    facade.load();
    expect(facade.featuredPlugin()).toBeNull();
  });

  it('should set isLoading to false even when result is null', () => {
    const { facade } = setupWithNull();
    facade.load();
    expect(facade.isLoading()).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// load() — immutability (new object on each load)
// ---------------------------------------------------------------------------

describe('FeaturedPluginFacade — immutable updates', () => {
  it('should return a new plugin object on each successive load', () => {
    let call = 0;
    @Injectable()
    class SequentialPort extends FeaturedPluginPort {
      override getFeaturedPlugin(): Observable<FeaturedPlugin | null> {
        call++;
        return of({
          pluginId: `p${call}`,
          name: `Plugin ${call}`,
          slug: `plugin-${call}`,
          latestVersion: `${call}.0.0`,
        });
      }
    }

    TestBed.configureTestingModule({
      providers: [FeaturedPluginFacade, { provide: FeaturedPluginPort, useClass: SequentialPort }],
    });
    const facade = TestBed.inject(FeaturedPluginFacade);

    facade.load();
    const first = facade.featuredPlugin();
    facade.load();
    const second = facade.featuredPlugin();

    expect(second?.slug).toBe('plugin-2');
    expect(first?.slug).toBe('plugin-1');
  });
});
