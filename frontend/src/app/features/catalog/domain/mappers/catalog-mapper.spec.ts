/**
 * RED tests — Task 12.1 / 12.2: Domain models + mappers
 *
 * Expected production files (DO NOT exist yet — tests will FAIL to compile):
 *   src/app/features/catalog/domain/models/catalog.models.ts
 *   src/app/features/catalog/domain/mappers/catalog-mapper.ts
 *
 * Production types the coder MUST define:
 *
 *   // catalog.models.ts
 *   type AddOnVersion = {
 *     readonly pluginId: string;
 *     readonly version: string;
 *     readonly isLatest: boolean;
 *     readonly downloadCount: number;
 *     readonly releaseNotes: string;
 *     readonly createdAt: Date;
 *   }
 *
 *   type AddOnSummary = {
 *     readonly pluginId: string;
 *     readonly name: string;
 *     readonly slug: string;
 *     readonly description: string;
 *     readonly author: string;
 *     readonly types: readonly string[];
 *     readonly languages: readonly string[];
 *     readonly useCaseTags: readonly string[];
 *     readonly downloadCount: number;
 *     readonly latestVersion: string | null;
 *     readonly createdAt: Date;
 *     readonly updatedAt: Date;
 *   }
 *
 *   type AddOnDetail = AddOnSummary & {
 *     readonly versions: readonly AddOnVersion[];
 *   }
 *
 *   type CategoryValue = {
 *     readonly value: string;
 *     readonly displayName: string;
 *     readonly description: string;
 *     readonly count: number;
 *   }
 *
 *   type Categories = {
 *     readonly types: readonly CategoryValue[];
 *     readonly languages: readonly CategoryValue[];
 *     readonly useCases: readonly CategoryValue[];
 *   }
 *
 *   type PaginationMeta = {
 *     readonly totalCount: number;
 *     readonly page: number;
 *     readonly limit: number;
 *     readonly totalPages: number;
 *   }
 *
 *   // catalog-mapper.ts
 *   function mapAddOnVersionDtoToAddOnVersion(dto: AddOnVersionDto): AddOnVersion
 *   function mapAddOnDtoToAddOnSummary(dto: AddOnDto): AddOnSummary
 *   function mapAddOnDtoToAddOnDetail(dto: AddOnDto): AddOnDetail
 *   function mapCategoryValueDtoToCategoryValue(dto: CategoryValueDto): CategoryValue
 *   function mapCategoriesDtoToCategories(dto: CategoriesDto): Categories
 *   function mapPaginatedEnvelopeToMeta<T>(envelope: PaginatedEnvelope<T>): PaginationMeta
 */

import {
  mapCategoriesDtoToCategories,
  mapCategoryValueDtoToCategoryValue,
  mapPaginatedEnvelopeToMeta,
  mapAddOnDtoToAddOnDetail,
  mapAddOnDtoToAddOnSummary,
  mapAddOnVersionDtoToAddOnVersion,
} from './catalog-mapper';
import type {
  Categories,
  CategoryValue,
  PaginationMeta,
  AddOnDetail,
  AddOnSummary,
  AddOnVersion,
} from '../models/catalog.models';
import type {
  CategoriesDto,
  CategoryValueDto,
  PaginatedEnvelope,
  AddOnDto,
  AddOnVersionDto,
} from '../../../../shared/infrastructure/http/api-client.types';

// ---------------------------------------------------------------------------
// DTO Fixtures
// ---------------------------------------------------------------------------

const versionDto: AddOnVersionDto = {
  pluginId: 'plugin-abc',
  version: '2.1.0',
  isLatest: true,
  downloadCount: 4200,
  releaseNotes: 'Improved stability.',
  createdAt: '2024-03-15T12:00:00.000Z',
};

const pluginDto: AddOnDto = {
  pluginId: 'plugin-abc',
  name: 'Awesome Plugin',
  slug: 'awesome-plugin',
  description: 'Does awesome things.',
  author: 'Jane Dev',
  types: ['formatter', 'linter'],
  languages: ['typescript', 'javascript'],
  useCaseTags: ['code-quality'],
  downloadCount: 10000,
  latestVersion: '2.1.0',
  versions: [versionDto],
  createdAt: '2023-01-01T00:00:00.000Z',
  updatedAt: '2024-03-15T12:00:00.000Z',
};

const categoryValueDto: CategoryValueDto = {
  value: 'typescript',
  displayName: 'TypeScript',
  description: 'TypeScript language plugins.',
  count: 42,
};

const categoriesDto: CategoriesDto = {
  types: [{ value: 'formatter', displayName: 'Formatter', description: 'Formatting tools.', count: 10 }],
  languages: [categoryValueDto],
  useCases: [{ value: 'code-quality', displayName: 'Code Quality', description: 'Quality tools.', count: 5 }],
};

const paginatedEnvelope: PaginatedEnvelope<AddOnDto> = {
  data: [pluginDto],
  totalCount: 1,
  page: 1,
  limit: 20,
  totalPages: 1,
};

// ---------------------------------------------------------------------------
// mapAddOnVersionDtoToAddOnVersion
// ---------------------------------------------------------------------------

describe('mapAddOnVersionDtoToAddOnVersion', () => {
  let result: AddOnVersion;

  beforeEach(() => {
    result = mapAddOnVersionDtoToAddOnVersion(versionDto);
  });

  it('should map pluginId', () => {
    expect(result.pluginId).toBe('plugin-abc');
  });

  it('should map version string', () => {
    expect(result.version).toBe('2.1.0');
  });

  it('should map isLatest', () => {
    expect(result.isLatest).toBe(true);
  });

  it('should map downloadCount', () => {
    expect(result.downloadCount).toBe(4200);
  });

  it('should map releaseNotes', () => {
    expect(result.releaseNotes).toBe('Improved stability.');
  });

  it('should convert createdAt string to a Date object', () => {
    expect(result.createdAt).toBeInstanceOf(Date);
    expect(result.createdAt.toISOString()).toBe('2024-03-15T12:00:00.000Z');
  });

  it('should return a new object each call (immutability — no shared reference)', () => {
    const r1 = mapAddOnVersionDtoToAddOnVersion(versionDto);
    const r2 = mapAddOnVersionDtoToAddOnVersion(versionDto);
    expect(r1).not.toBe(r2);
  });

  it('should not mutate the source DTO', () => {
    const copy: AddOnVersionDto = { ...versionDto };
    mapAddOnVersionDtoToAddOnVersion(versionDto);
    expect(versionDto).toEqual(copy);
  });
});

// ---------------------------------------------------------------------------
// mapAddOnDtoToAddOnSummary
// ---------------------------------------------------------------------------

describe('mapAddOnDtoToAddOnSummary', () => {
  let result: AddOnSummary;

  beforeEach(() => {
    result = mapAddOnDtoToAddOnSummary(pluginDto);
  });

  it('should map pluginId', () => {
    expect(result.pluginId).toBe('plugin-abc');
  });

  it('should map name', () => {
    expect(result.name).toBe('Awesome Plugin');
  });

  it('should map slug', () => {
    expect(result.slug).toBe('awesome-plugin');
  });

  it('should map description', () => {
    expect(result.description).toBe('Does awesome things.');
  });

  it('should map author', () => {
    expect(result.author).toBe('Jane Dev');
  });

  it('should map types array', () => {
    expect(result.types).toEqual(['formatter', 'linter']);
  });

  it('should map languages array', () => {
    expect(result.languages).toEqual(['typescript', 'javascript']);
  });

  it('should map useCaseTags array', () => {
    expect(result.useCaseTags).toEqual(['code-quality']);
  });

  it('should map downloadCount', () => {
    expect(result.downloadCount).toBe(10000);
  });

  it('should map latestVersion (non-null)', () => {
    expect(result.latestVersion).toBe('2.1.0');
  });

  it('should map latestVersion when null', () => {
    const nullVersionDto: AddOnDto = { ...pluginDto, latestVersion: null };
    const r = mapAddOnDtoToAddOnSummary(nullVersionDto);
    expect(r.latestVersion).toBeNull();
  });

  it('should convert createdAt string to a Date', () => {
    expect(result.createdAt).toBeInstanceOf(Date);
  });

  it('should convert updatedAt string to a Date', () => {
    expect(result.updatedAt).toBeInstanceOf(Date);
  });

  it('should NOT include a versions field (summary excludes version history)', () => {
    expect((result as Record<string, unknown>)['versions']).toBeUndefined();
  });

  it('should return a new object each call', () => {
    const r1 = mapAddOnDtoToAddOnSummary(pluginDto);
    const r2 = mapAddOnDtoToAddOnSummary(pluginDto);
    expect(r1).not.toBe(r2);
  });

  it('should not mutate the source DTO', () => {
    const originalTypes = [...pluginDto.types];
    mapAddOnDtoToAddOnSummary(pluginDto);
    expect(pluginDto.types).toEqual(originalTypes);
  });
});

// ---------------------------------------------------------------------------
// mapAddOnDtoToAddOnDetail
// ---------------------------------------------------------------------------

describe('mapAddOnDtoToAddOnDetail', () => {
  let result: AddOnDetail;

  beforeEach(() => {
    result = mapAddOnDtoToAddOnDetail(pluginDto);
  });

  it('should include all AddOnSummary fields', () => {
    expect(result.pluginId).toBe('plugin-abc');
    expect(result.name).toBe('Awesome Plugin');
    expect(result.author).toBe('Jane Dev');
  });

  it('should include a versions array', () => {
    expect(result.versions).toHaveLength(1);
  });

  it('should map each version in the versions array', () => {
    const v = result.versions[0];
    expect(v.version).toBe('2.1.0');
    expect(v.isLatest).toBe(true);
    expect(v.createdAt).toBeInstanceOf(Date);
  });

  it('should map empty versions array gracefully', () => {
    const noVersionsDto: AddOnDto = { ...pluginDto, versions: [] };
    const r = mapAddOnDtoToAddOnDetail(noVersionsDto);
    expect(r.versions).toEqual([]);
  });

  it('should return a new object each call', () => {
    const r1 = mapAddOnDtoToAddOnDetail(pluginDto);
    const r2 = mapAddOnDtoToAddOnDetail(pluginDto);
    expect(r1).not.toBe(r2);
  });
});

// ---------------------------------------------------------------------------
// mapCategoryValueDtoToCategoryValue
// ---------------------------------------------------------------------------

describe('mapCategoryValueDtoToCategoryValue', () => {
  let result: CategoryValue;

  beforeEach(() => {
    result = mapCategoryValueDtoToCategoryValue(categoryValueDto);
  });

  it('should map value', () => {
    expect(result.value).toBe('typescript');
  });

  it('should map displayName', () => {
    expect(result.displayName).toBe('TypeScript');
  });

  it('should map description', () => {
    expect(result.description).toBe('TypeScript language plugins.');
  });

  it('should map count', () => {
    expect(result.count).toBe(42);
  });

  it('should return a new object each call', () => {
    const r1 = mapCategoryValueDtoToCategoryValue(categoryValueDto);
    const r2 = mapCategoryValueDtoToCategoryValue(categoryValueDto);
    expect(r1).not.toBe(r2);
  });
});

// ---------------------------------------------------------------------------
// mapCategoriesDtoToCategories
// ---------------------------------------------------------------------------

describe('mapCategoriesDtoToCategories', () => {
  let result: Categories;

  beforeEach(() => {
    result = mapCategoriesDtoToCategories(categoriesDto);
  });

  it('should map types array', () => {
    expect(result.types).toHaveLength(1);
    expect(result.types[0].value).toBe('formatter');
  });

  it('should map languages array', () => {
    expect(result.languages).toHaveLength(1);
    expect(result.languages[0].value).toBe('typescript');
  });

  it('should map useCases array', () => {
    expect(result.useCases).toHaveLength(1);
    expect(result.useCases[0].value).toBe('code-quality');
  });

  it('should map empty arrays gracefully', () => {
    const empty: CategoriesDto = { types: [], languages: [], useCases: [] };
    const r = mapCategoriesDtoToCategories(empty);
    expect(r.types).toEqual([]);
    expect(r.languages).toEqual([]);
    expect(r.useCases).toEqual([]);
  });

  it('should return a new object each call', () => {
    const r1 = mapCategoriesDtoToCategories(categoriesDto);
    const r2 = mapCategoriesDtoToCategories(categoriesDto);
    expect(r1).not.toBe(r2);
  });
});

// ---------------------------------------------------------------------------
// mapPaginatedEnvelopeToMeta
// ---------------------------------------------------------------------------

describe('mapPaginatedEnvelopeToMeta', () => {
  let result: PaginationMeta;

  beforeEach(() => {
    result = mapPaginatedEnvelopeToMeta(paginatedEnvelope);
  });

  it('should map totalCount', () => {
    expect(result.totalCount).toBe(1);
  });

  it('should map page', () => {
    expect(result.page).toBe(1);
  });

  it('should map limit', () => {
    expect(result.limit).toBe(20);
  });

  it('should map totalPages', () => {
    expect(result.totalPages).toBe(1);
  });

  it('should handle multi-page envelopes', () => {
    const multiPage: PaginatedEnvelope<AddOnDto> = {
      data: [],
      totalCount: 100,
      page: 3,
      limit: 10,
      totalPages: 10,
    };
    const r = mapPaginatedEnvelopeToMeta(multiPage);
    expect(r.totalCount).toBe(100);
    expect(r.page).toBe(3);
    expect(r.totalPages).toBe(10);
  });

  it('should return a new object each call', () => {
    const r1 = mapPaginatedEnvelopeToMeta(paginatedEnvelope);
    const r2 = mapPaginatedEnvelopeToMeta(paginatedEnvelope);
    expect(r1).not.toBe(r2);
  });
});

// ---------------------------------------------------------------------------
// Immutability — frozen-like semantics: mapped arrays must be independent
// ---------------------------------------------------------------------------

describe('Mapper immutability — array independence', () => {
  it('mutating the mapped types array must not affect the original DTO', () => {
    const r = mapAddOnDtoToAddOnSummary(pluginDto);
    const originalLength = pluginDto.types.length;
    (r.types as string[]).push('injected');
    expect(pluginDto.types).toHaveLength(originalLength);
  });

  it('mutating the mapped versions array must not affect the original DTO', () => {
    const r = mapAddOnDtoToAddOnDetail(pluginDto);
    const originalLength = pluginDto.versions.length;
    (r.versions as AddOnVersion[]).push({ ...r.versions[0] });
    expect(pluginDto.versions).toHaveLength(originalLength);
  });
});
