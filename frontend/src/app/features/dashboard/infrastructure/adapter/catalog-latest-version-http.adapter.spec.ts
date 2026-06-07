/**
 * Unit tests for CatalogLatestVersionHttpAdapter.
 */

import { TestBed } from '@angular/core/testing';
import { Injectable } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';
import { CatalogLatestVersionHttpAdapter } from './catalog-latest-version-http.adapter';
import { CatalogLatestVersionPort } from '../../domain/ports/catalog-latest-version.port';
import { ApiClient } from '../../../../shared/infrastructure/http/api-client';
import type { PaginatedEnvelope, SearchResultDto } from '../../../../shared/infrastructure/http/api-client.types';

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

function makeSearchResult(name: string, latestVersion: string | null): SearchResultDto {
  return {
    pluginId: `p-${name}`,
    name,
    slug: name.toLowerCase(),
    description: 'desc',
    author: 'author',
    types: [],
    languages: [],
    downloadCount: 100,
    latestVersion,
    relevanceScore: 0.9,
  };
}

function makeEnvelope(data: SearchResultDto[]): PaginatedEnvelope<SearchResultDto> {
  return { data, totalCount: data.length, page: 1, limit: 1, totalPages: 1 };
}

// ---------------------------------------------------------------------------
// Stub ApiClient
// ---------------------------------------------------------------------------

@Injectable()
class StubApiClient {
  searchPluginsResult: Observable<PaginatedEnvelope<SearchResultDto>> = of(makeEnvelope([]));

  searchPlugins(_params: Parameters<ApiClient['searchPlugins']>[0]): Observable<PaginatedEnvelope<SearchResultDto>> {
    return this.searchPluginsResult;
  }
}

// ---------------------------------------------------------------------------
// Setup
// ---------------------------------------------------------------------------

function setup(): { adapter: CatalogLatestVersionHttpAdapter; stub: StubApiClient } {
  TestBed.resetTestingModule();
  const stub = new StubApiClient();
  TestBed.configureTestingModule({
    providers: [
      { provide: ApiClient, useValue: stub },
      { provide: CatalogLatestVersionPort, useClass: CatalogLatestVersionHttpAdapter },
      CatalogLatestVersionHttpAdapter,
    ],
  });
  return { adapter: TestBed.inject(CatalogLatestVersionHttpAdapter), stub };
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe('CatalogLatestVersionHttpAdapter — getLatestVersion', () => {
  it('should return latestVersion when plugin is found by exact name match (case-insensitive)', () => {
    const { adapter, stub } = setup();
    stub.searchPluginsResult = of(makeEnvelope([makeSearchResult('MyPlugin', '1.2.3')]));
    let result: string | null | undefined;
    adapter.getLatestVersion('myplugin').subscribe((v) => (result = v));
    expect(result).toBe('1.2.3');
  });

  it('should return null when no plugin matches the given name', () => {
    const { adapter, stub } = setup();
    stub.searchPluginsResult = of(makeEnvelope([makeSearchResult('OtherPlugin', '1.0.0')]));
    let result: string | null | undefined;
    adapter.getLatestVersion('MyPlugin').subscribe((v) => (result = v));
    expect(result).toBeNull();
  });

  it('should return null when the search result is empty', () => {
    const { adapter, stub } = setup();
    stub.searchPluginsResult = of(makeEnvelope([]));
    let result: string | null | undefined;
    adapter.getLatestVersion('anything').subscribe((v) => (result = v));
    expect(result).toBeNull();
  });

  it('should return null when latestVersion is null on the matching plugin', () => {
    const { adapter, stub } = setup();
    stub.searchPluginsResult = of(makeEnvelope([makeSearchResult('MyPlugin', null)]));
    let result: string | null | undefined;
    adapter.getLatestVersion('MyPlugin').subscribe((v) => (result = v));
    expect(result).toBeNull();
  });

  it('should return null (not throw) when the API call errors', () => {
    const { adapter, stub } = setup();
    stub.searchPluginsResult = throwError(() => new Error('Network error'));
    let result: string | null | undefined;
    let errorCaught = false;
    adapter.getLatestVersion('MyPlugin').subscribe({
      next: (v) => (result = v),
      error: () => (errorCaught = true),
    });
    expect(errorCaught).toBe(false);
    expect(result).toBeNull();
  });

  it('should match case-insensitively (uppercase plugin name vs lowercase result)', () => {
    const { adapter, stub } = setup();
    stub.searchPluginsResult = of(makeEnvelope([makeSearchResult('myplugin', '3.0.0')]));
    let result: string | null | undefined;
    adapter.getLatestVersion('MYPLUGIN').subscribe((v) => (result = v));
    expect(result).toBe('3.0.0');
  });

  it('should be an instance of CatalogLatestVersionPort', () => {
    const { adapter } = setup();
    expect(adapter).toBeInstanceOf(CatalogLatestVersionPort);
  });
});
