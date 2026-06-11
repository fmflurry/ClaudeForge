/**
 * Pure mapping functions from API DTOs to domain models.
 * All functions are immutable — they return new objects and never mutate inputs.
 */

import type {
  AddOnDto,
  AddOnVersionDto,
  CategoriesDto,
  CategoryValueDto,
  PaginatedEnvelope,
} from '../../../../shared/infrastructure/http/api-client.types';
import type {
  AddOnDetail,
  AddOnSummary,
  AddOnVersion,
  Categories,
  CategoryValue,
  PaginationMeta,
} from '../models/catalog.models';

export function mapAddOnVersionDtoToAddOnVersion(dto: AddOnVersionDto): AddOnVersion {
  return {
    pluginId: dto.pluginId,
    version: dto.version,
    isLatest: dto.isLatest,
    downloadCount: dto.downloadCount,
    releaseNotes: dto.releaseNotes,
    createdAt: new Date(dto.createdAt),
  };
}

export function mapAddOnDtoToAddOnSummary(dto: AddOnDto): AddOnSummary {
  return {
    pluginId: dto.pluginId,
    name: dto.name,
    slug: dto.slug,
    description: dto.description,
    author: dto.author,
    types: [...(dto.types ?? [])],
    languages: [...(dto.languages ?? [])],
    useCaseTags: [...(dto.useCaseTags ?? [])],
    downloadCount: dto.downloadCount,
    latestVersion: dto.latestVersion,
    createdAt: new Date(dto.createdAt),
    updatedAt: new Date(dto.updatedAt),
  };
}

export function mapAddOnDtoToAddOnDetail(dto: AddOnDto): AddOnDetail {
  return {
    ...mapAddOnDtoToAddOnSummary(dto),
    versions: dto.versions.map(mapAddOnVersionDtoToAddOnVersion),
  };
}

export function mapCategoryValueDtoToCategoryValue(dto: CategoryValueDto): CategoryValue {
  return {
    value: dto.value,
    displayName: dto.displayName,
    description: dto.description,
    count: dto.count,
  };
}

export function mapCategoriesDtoToCategories(dto: CategoriesDto): Categories {
  return {
    types: dto.types.map(mapCategoryValueDtoToCategoryValue),
    languages: dto.languages.map(mapCategoryValueDtoToCategoryValue),
    useCases: dto.useCases.map(mapCategoryValueDtoToCategoryValue),
  };
}

export function mapPaginatedEnvelopeToMeta<T>(envelope: PaginatedEnvelope<T>): PaginationMeta {
  return {
    totalCount: envelope.totalCount,
    page: envelope.page,
    limit: envelope.limit,
    totalPages: envelope.totalPages,
  };
}
