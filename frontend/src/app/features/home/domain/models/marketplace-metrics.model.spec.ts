/**
 * RED tests — Home metrics domain model.
 *
 * Production file that MUST exist (DO NOT create yet):
 *   src/app/features/home/domain/models/marketplace-metrics.model.ts
 *
 * The coder MUST export:
 *
 *   // marketplace-metrics.model.ts
 *   export type MarketplaceMetrics = {
 *     readonly totalPlugins: number;
 *     readonly totalDownloads: number;
 *     readonly publisherCount: number;
 *     readonly categoryCount: number;
 *   };
 *
 *   // Companion Zod schema used at the HTTP adapter boundary:
 *   export const marketplaceMetricsSchema = z.object({
 *     totalPlugins: z.number().int().nonnegative(),
 *     totalDownloads: z.number().int().nonnegative(),
 *     publisherCount: z.number().int().nonnegative(),
 *     categoryCount: z.number().int().nonnegative(),
 *   });
 *
 * All four fields are required integers and must be readonly on the type.
 */

import type { MarketplaceMetrics } from './marketplace-metrics.model';
import { marketplaceMetricsSchema } from './marketplace-metrics.model';

// The real schema returns a Zod SafeParseReturnType; the stub always returns
// { success: false }. We cast through unknown so the spec compiles against
// both the stub and the real Zod schema.
type SchemaParseResult = { success: true; data: MarketplaceMetrics } | { success: false; error: unknown };

// ---------------------------------------------------------------------------
// Model shape
// ---------------------------------------------------------------------------

describe('MarketplaceMetrics — model shape', () => {
  it('should accept a well-formed metrics object', () => {
    const metrics: MarketplaceMetrics = {
      totalPlugins: 42,
      totalDownloads: 1_234,
      publisherCount: 10,
      categoryCount: 5,
    };
    expect(metrics.totalPlugins).toBe(42);
    expect(metrics.totalDownloads).toBe(1_234);
    expect(metrics.publisherCount).toBe(10);
    expect(metrics.categoryCount).toBe(5);
  });

  it('should expose totalPlugins as a number', () => {
    const metrics: MarketplaceMetrics = {
      totalPlugins: 0,
      totalDownloads: 0,
      publisherCount: 0,
      categoryCount: 0,
    };
    expect(typeof metrics.totalPlugins).toBe('number');
  });

  it('should expose totalDownloads as a number', () => {
    const metrics: MarketplaceMetrics = {
      totalPlugins: 0,
      totalDownloads: 0,
      publisherCount: 0,
      categoryCount: 0,
    };
    expect(typeof metrics.totalDownloads).toBe('number');
  });

  it('should expose publisherCount as a number', () => {
    const metrics: MarketplaceMetrics = {
      totalPlugins: 0,
      totalDownloads: 0,
      publisherCount: 0,
      categoryCount: 0,
    };
    expect(typeof metrics.publisherCount).toBe('number');
  });

  it('should expose categoryCount as a number', () => {
    const metrics: MarketplaceMetrics = {
      totalPlugins: 0,
      totalDownloads: 0,
      publisherCount: 0,
      categoryCount: 0,
    };
    expect(typeof metrics.categoryCount).toBe('number');
  });
});

// ---------------------------------------------------------------------------
// Runtime schema validation (Zod)
// ---------------------------------------------------------------------------

describe('marketplaceMetricsSchema — valid payloads', () => {
  it('should parse a valid DTO with all integer fields', () => {
    const result = marketplaceMetricsSchema.safeParse({
      totalPlugins: 10,
      totalDownloads: 500,
      publisherCount: 3,
      categoryCount: 7,
    }) as SchemaParseResult;
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data.totalPlugins).toBe(10);
    }
  });

  it('should parse zero values correctly', () => {
    const result = marketplaceMetricsSchema.safeParse({
      totalPlugins: 0,
      totalDownloads: 0,
      publisherCount: 0,
      categoryCount: 0,
    }) as SchemaParseResult;
    expect(result.success).toBe(true);
  });

  it('should parse large integer values without overflow', () => {
    const result = marketplaceMetricsSchema.safeParse({
      totalPlugins: 999_999,
      totalDownloads: 10_000_000,
      publisherCount: 50_000,
      categoryCount: 200,
    }) as SchemaParseResult;
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data.totalDownloads).toBe(10_000_000);
    }
  });
});

describe('marketplaceMetricsSchema — invalid payloads', () => {
  it('should reject a payload missing totalPlugins', () => {
    const result = marketplaceMetricsSchema.safeParse({
      totalDownloads: 500,
      publisherCount: 3,
      categoryCount: 7,
    });
    expect(result.success).toBe(false);
  });

  it('should reject a payload missing totalDownloads', () => {
    const result = marketplaceMetricsSchema.safeParse({
      totalPlugins: 10,
      publisherCount: 3,
      categoryCount: 7,
    });
    expect(result.success).toBe(false);
  });

  it('should reject a payload missing publisherCount', () => {
    const result = marketplaceMetricsSchema.safeParse({
      totalPlugins: 10,
      totalDownloads: 500,
      categoryCount: 7,
    });
    expect(result.success).toBe(false);
  });

  it('should reject a payload missing categoryCount', () => {
    const result = marketplaceMetricsSchema.safeParse({
      totalPlugins: 10,
      totalDownloads: 500,
      publisherCount: 3,
    });
    expect(result.success).toBe(false);
  });

  it('should reject a payload with a string totalPlugins (non-numeric)', () => {
    const result = marketplaceMetricsSchema.safeParse({
      totalPlugins: 'not-a-number',
      totalDownloads: 500,
      publisherCount: 3,
      categoryCount: 7,
    });
    expect(result.success).toBe(false);
  });

  it('should reject a null payload', () => {
    const result = marketplaceMetricsSchema.safeParse(null);
    expect(result.success).toBe(false);
  });

  it('should reject an empty object', () => {
    const result = marketplaceMetricsSchema.safeParse({});
    expect(result.success).toBe(false);
  });

  it('should reject a payload where totalPlugins is negative', () => {
    const result = marketplaceMetricsSchema.safeParse({
      totalPlugins: -1,
      totalDownloads: 500,
      publisherCount: 3,
      categoryCount: 7,
    });
    expect(result.success).toBe(false);
  });

  it('should reject a payload where totalDownloads is a float', () => {
    const result = marketplaceMetricsSchema.safeParse({
      totalPlugins: 10,
      totalDownloads: 3.14,
      publisherCount: 3,
      categoryCount: 7,
    });
    expect(result.success).toBe(false);
  });
});
