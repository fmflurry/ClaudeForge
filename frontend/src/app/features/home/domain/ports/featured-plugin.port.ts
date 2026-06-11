import { Observable } from 'rxjs';
import type { FeaturedAddOn } from '../models/featured-plugin.model';

/**
 * Port for fetching the currently featured add-on.
 * Returns null when no add-on is featured or when the fetch fails.
 */
export abstract class FeaturedAddOnPort {
  abstract getFeaturedAddOn(): Observable<FeaturedAddOn | null>;
}
