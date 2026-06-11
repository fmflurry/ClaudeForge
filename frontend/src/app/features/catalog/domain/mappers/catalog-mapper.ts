/**
 * Pure mapping functions from API DTOs to domain models.
 * All functions are immutable — they return new objects and never mutate inputs.
 */

import type {
  CategoriesDto,
  CategoryValueDto,
  PaginatedEnvelope,
  PluginDto,
  PluginVersionDto,
} from '../../../../shared/infrastructure/http/api-client.types';
import type {
  Categories,
  CategoryValue,
  PaginationMeta,
  PluginDetail,
  PluginSummary,
  PluginVersion,
} from '../models/catalog.models';

export function mapPluginVersionDtoToPluginVersion(dto: PluginVersionDto): PluginVersion {
  return {
    pluginId: dto.pluginId,
    version: dto.version,
    isLatest: dto.isLatest,
    downloadCount: dto.downloadCount,
    releaseNotes: dto.releaseNotes,
    createdAt: new Date(dto.createdAt),
  };
}

export function mapPluginDtoToPluginSummary(dto: PluginDto): PluginSummary {
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

export function mapPluginDtoToPluginDetail(dto: PluginDto): PluginDetail {
  return {
    ...mapPluginDtoToPluginSummary(dto),
    versions: dto.versions.map(mapPluginVersionDtoToPluginVersion),
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
