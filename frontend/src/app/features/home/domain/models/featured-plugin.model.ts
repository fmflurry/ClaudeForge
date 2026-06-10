/**
 * Domain model for a featured plugin.
 * Immutable — all fields are readonly.
 */
export interface FeaturedPlugin {
  readonly pluginId: string;
  readonly name: string;
  readonly slug: string;
  readonly latestVersion: string | null;
}
