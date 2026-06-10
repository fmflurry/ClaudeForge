import { Observable } from 'rxjs';
import type { FeaturedPlugin } from '../models/featured-plugin.model';

/**
 * Port for fetching the currently featured plugin.
 * Returns null when no plugin is featured or when the fetch fails.
 */
export abstract class FeaturedPluginPort {
  abstract getFeaturedPlugin(): Observable<FeaturedPlugin | null>;
}
