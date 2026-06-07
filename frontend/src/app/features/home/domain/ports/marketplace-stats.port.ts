import { Observable } from 'rxjs';
import type { MarketplaceMetrics } from '../models/marketplace-metrics.model';

export abstract class MarketplaceStatsPort {
  abstract getStats(): Observable<MarketplaceMetrics>;
}
