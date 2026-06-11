/**
 * Tests for FeaturedAddOnFacade.
 * Follows the pattern of home-metrics.facade.spec.ts.
 */

import { TestBed } from '@angular/core/testing';
import { Injectable } from '@angular/core';
import { Observable, of } from 'rxjs';
import { FeaturedAddOnFacade } from './featured-plugin.facade';
import { FeaturedAddOnPort } from '../../domain/ports/featured-plugin.port';
import type { FeaturedAddOn } from '../../domain/models/featured-plugin.model';

// ---------------------------------------------------------------------------
// Fake port implementations
// ---------------------------------------------------------------------------

const FAKE_ADDON: FeaturedAddOn = {
  pluginId: 'plugin-abc',
  name: 'Awesome AddOn',
  slug: 'awesome-addon',
  latestVersion: '1.2.3',
};

@Injectable()
class FakeFeaturedAddOnPort extends FeaturedAddOnPort {
  override getFeaturedAddOn(): Observable<FeaturedAddOn | null> {
    return of(FAKE_ADDON);
  }
}

@Injectable()
class NullFeaturedAddOnPort extends FeaturedAddOnPort {
  override getFeaturedAddOn(): Observable<FeaturedAddOn | null> {
    return of(null);
  }
}

// ---------------------------------------------------------------------------
// Setup helpers
// ---------------------------------------------------------------------------

function setupWithAddOn(): { facade: FeaturedAddOnFacade } {
  TestBed.configureTestingModule({
    providers: [FeaturedAddOnFacade, { provide: FeaturedAddOnPort, useClass: FakeFeaturedAddOnPort }],
  });
  return { facade: TestBed.inject(FeaturedAddOnFacade) };
}

function setupWithNull(): { facade: FeaturedAddOnFacade } {
  TestBed.configureTestingModule({
    providers: [FeaturedAddOnFacade, { provide: FeaturedAddOnPort, useClass: NullFeaturedAddOnPort }],
  });
  return { facade: TestBed.inject(FeaturedAddOnFacade) };
}

// ---------------------------------------------------------------------------
// Initial state
// ---------------------------------------------------------------------------

describe('FeaturedAddOnFacade — initial state', () => {
  it('featuredAddOn() should be null before load()', () => {
    const { facade } = setupWithAddOn();
    expect(facade.featuredAddOn()).toBeNull();
  });

  it('isLoading() should be false before load()', () => {
    const { facade } = setupWithAddOn();
    expect(facade.isLoading()).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// Signal API surface
// ---------------------------------------------------------------------------

describe('FeaturedAddOnFacade — signal API surface', () => {
  it('featuredAddOn should be a callable signal function', () => {
    const { facade } = setupWithAddOn();
    expect(typeof facade.featuredAddOn).toBe('function');
  });

  it('isLoading should be a callable signal function', () => {
    const { facade } = setupWithAddOn();
    expect(typeof facade.isLoading).toBe('function');
  });

  it('load should be a method', () => {
    const { facade } = setupWithAddOn();
    expect(typeof facade.load).toBe('function');
  });
});

// ---------------------------------------------------------------------------
// load() — success (featured add-on present)
// ---------------------------------------------------------------------------

describe('FeaturedAddOnFacade — load() with add-on', () => {
  it('should set featuredAddOn() after successful load', () => {
    const { facade } = setupWithAddOn();
    facade.load();
    expect(facade.featuredAddOn()).toEqual(FAKE_ADDON);
  });

  it('should set isLoading to false after load', () => {
    const { facade } = setupWithAddOn();
    facade.load();
    expect(facade.isLoading()).toBe(false);
  });

  it('should expose the slug from the featured add-on', () => {
    const { facade } = setupWithAddOn();
    facade.load();
    expect(facade.featuredAddOn()?.slug).toBe('awesome-addon');
  });

  it('should expose the latestVersion from the featured add-on', () => {
    const { facade } = setupWithAddOn();
    facade.load();
    expect(facade.featuredAddOn()?.latestVersion).toBe('1.2.3');
  });
});

// ---------------------------------------------------------------------------
// load() — null result (none featured / fetch failed → adapter returns null)
// ---------------------------------------------------------------------------

describe('FeaturedAddOnFacade — load() with null (no featured add-on)', () => {
  it('should set featuredAddOn() to null when port returns null', () => {
    const { facade } = setupWithNull();
    facade.load();
    expect(facade.featuredAddOn()).toBeNull();
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

describe('FeaturedAddOnFacade — immutable updates', () => {
  it('should return a new add-on object on each successive load', () => {
    let call = 0;
    @Injectable()
    class SequentialPort extends FeaturedAddOnPort {
      override getFeaturedAddOn(): Observable<FeaturedAddOn | null> {
        call++;
        return of({
          pluginId: `p${call}`,
          name: `AddOn ${call}`,
          slug: `addon-${call}`,
          latestVersion: `${call}.0.0`,
        });
      }
    }

    TestBed.configureTestingModule({
      providers: [FeaturedAddOnFacade, { provide: FeaturedAddOnPort, useClass: SequentialPort }],
    });
    const facade = TestBed.inject(FeaturedAddOnFacade);

    facade.load();
    const first = facade.featuredAddOn();
    facade.load();
    const second = facade.featuredAddOn();

    expect(second?.slug).toBe('addon-2');
    expect(first?.slug).toBe('addon-1');
  });
});
