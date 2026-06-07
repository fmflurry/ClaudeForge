import { z } from 'zod';

export interface MarketplaceMetrics {
  readonly totalPlugins: number;
  readonly totalDownloads: number;
  readonly publisherCount: number;
  readonly categoryCount: number;
}

export const marketplaceMetricsSchema = z.object({
  totalPlugins: z.number().int().nonnegative(),
  totalDownloads: z.number().int().nonnegative(),
  publisherCount: z.number().int().nonnegative(),
  categoryCount: z.number().int().nonnegative(),
});
