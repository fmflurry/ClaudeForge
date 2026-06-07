import { inject, Injectable } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiClient } from '../../../../shared/infrastructure/http/api-client';
import { marketplaceMetricsSchema } from '../../domain/models/marketplace-metrics.model';
import type { MarketplaceMetrics } from '../../domain/models/marketplace-metrics.model';
import { MarketplaceStatsPort } from '../../domain/ports/marketplace-stats.port';

@Injectable()
export class MarketplaceStatsHttpAdapter extends MarketplaceStatsPort {
  private readonly apiClient = inject(ApiClient);

  override getStats(): Observable<MarketplaceMetrics> {
    return this.apiClient.getMarketplaceStats().pipe(map((dto) => marketplaceMetricsSchema.parse(dto)));
  }
}
