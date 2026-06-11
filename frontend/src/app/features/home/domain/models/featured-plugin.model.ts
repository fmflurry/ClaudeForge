/**
 * Domain model for a featured add-on.
 * Immutable — all fields are readonly.
 */
export interface FeaturedAddOn {
  readonly pluginId: string;
  readonly name: string;
  readonly slug: string;
  readonly latestVersion: string | null;
}
