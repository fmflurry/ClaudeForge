import {
  APP_INITIALIZER,
  ApplicationConfig,
  inject,
  isDevMode,
  PLATFORM_ID,
  provideBrowserGlobalErrorListeners,
  REQUEST,
} from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { provideClientHydration, withEventReplay, withHttpTransferCacheOptions } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { DOCUMENT } from '@angular/common';
import { provideTransloco } from '@jsverse/transloco';

import { routes } from './app.routes';
import { API_BASE_URL } from './core/config/api-config';
import { TranslocoTransferStateLoader } from './core/i18n/transloco-transfer-state.loader';
import { LanguageStoragePort } from './core/i18n/language-storage.port';
import { LocalStorageLanguageAdapter } from './core/i18n/local-storage-language.adapter';
import { I18nFacade } from './application/i18n/i18n.facade';
import { SERVER_ACTIVE_LANG } from './core/i18n/server-language.token';
import { LANG_VALUES } from './core/i18n/active-language';
import { parseAcceptLanguage, pickLanguage } from './core/i18n/language-detection';
import type { Lang } from './core/i18n/active-language';
import { TeamContextStoragePort } from './shared/domain/ports/team-context-storage.port';
import { LocalStorageTeamContextAdapter } from './shared/infrastructure/storage/local-storage-team-context.adapter';
import { InstalledPluginsStoragePort } from './shared/domain/ports/installed-plugins-storage.port';
import { LocalStorageInstalledPluginsAdapter } from './shared/infrastructure/storage/local-storage-installed-plugins.adapter';
import { TelemetryPreferencePort } from './shared/domain/ports/telemetry-preference.port';
import { LocalStorageTelemetryPreferenceAdapter } from './shared/infrastructure/storage/local-storage-telemetry-preference.adapter';
import { CatalogLatestVersionPort } from './features/dashboard/domain/ports/catalog-latest-version.port';
import { CatalogLatestVersionHttpAdapter } from './features/dashboard/infrastructure/adapter/catalog-latest-version-http.adapter';
import { CatalogPort } from './features/catalog/domain/ports/catalog.port';
import { CatalogHttpAdapter } from './features/catalog/infrastructure/adapter/catalog-http.adapter';
import { SearchPort } from './features/search/domain/ports/search.port';
import { SearchHttpAdapter } from './features/search/infrastructure/adapter/search-http.adapter';
import { CryptoPort } from './features/telemetry/domain/ports/crypto.port';
import { WebCryptoAdapter } from './features/telemetry/infrastructure/adapter/web-crypto.adapter';
import { AnonIdPort } from './features/telemetry/domain/ports/anon-id.port';
import { AnonIdAdapter } from './features/telemetry/infrastructure/adapter/anon-id.adapter';
import { DashboardFacade } from './features/dashboard/application/facades/dashboard.facade';
import { CatalogFacade } from './features/catalog/application/facades/catalog.facade';
import { SearchFacade } from './features/search/application/facades/search.facade';
import { TelemetryFacade } from './features/telemetry/application/facades/telemetry.facade';
import { DocsPort } from './features/docs/domain/ports/docs.port';
import { DocsHttpAdapter } from './features/docs/infrastructure/adapter/docs-http.adapter';
import { DocsFacade } from './features/docs/application/facades/docs.facade';
import { AuthPort } from './features/auth/domain/ports/auth.port';
import { AuthHttpAdapter } from './features/auth/infrastructure/adapter/auth-http.adapter';
import { AuthFacade } from './features/auth/application/facades/auth.facade';
import { authInterceptor } from './features/auth/infrastructure/interceptors/auth.interceptor';
import { OrgPort } from './features/organizations/domain/ports/org.port';
import { OrgHttpAdapter } from './features/organizations/infrastructure/adapter/org-http.adapter';
import { OrganizationsFacade } from './features/organizations/application/facades/organizations.facade';
import { OrgContextFacade } from './features/organizations/application/facades/org-context.facade';
import { DeviceActivationPort } from './features/device-activation/domain/ports/device-activation.port';
import { DeviceActivationHttpAdapter } from './features/device-activation/infrastructure/adapter/device-activation-http.adapter';
import { DeviceActivationFacade } from './features/device-activation/application/facades/device-activation.facade';
import { MarketplaceStatsPort } from './features/home/domain/ports/marketplace-stats.port';
import { MarketplaceStatsHttpAdapter } from './features/home/infrastructure/adapter/marketplace-stats-http.adapter';
import { HomeMetricsFacade } from './features/home/application/facades/home-metrics.facade';
import { provideZard } from './shared/core/provider/providezard';

/**
 * Reads the runtime API base URL from:
 * 1. SSR_API_BASE_URL environment variable (server-side only — absolute URL required for SSR).
 * 2. A <meta name="api-base-url"> tag injected by the server/nginx at deploy time (browser).
 * 3. Falls back to empty string (relative URLs) for local dev.
 *
 * Uses the injected DOCUMENT token so it works on both browser and server platforms.
 */
function resolveApiBaseUrl(doc: Document): string {
  const meta = doc.querySelector<HTMLMetaElement>('meta[name="api-base-url"]');
  if (meta?.content) return meta.content;
  return '';
}

/**
 * Factory for APP_INITIALIZER: attempts a silent token refresh on app boot.
 * A failure here is non-fatal — the user is simply unauthenticated.
 */
function silentRefreshInitializer(facade: AuthFacade): () => void {
  return () => facade.silentRefresh();
}

/**
 * APP_INITIALIZER factory: sets the active language based on platform.
 * - Browser: reads from localStorage, falls back to navigator.languages.
 * - Server: reads Accept-Language from the per-request REQUEST token.
 */
function i18nInitializer(
  facade: I18nFacade,
  platformId: object,
  storage: LanguageStoragePort,
  serverLang: Lang,
  request: Request | null,
): () => Promise<void> {
  return async () => {
    if (isPlatformBrowser(platformId)) {
      const stored = storage.read();
      if (stored) {
        facade.setLanguage(stored);
        await facade.load(stored);
        return;
      }
      // Fall back to browser navigator languages
      const navigatorLangs = typeof navigator !== 'undefined' ? (navigator.languages as readonly string[]) : [];
      const detected = pickLanguage(navigatorLangs, LANG_VALUES, 'en');
      facade.setLanguage(detected);
      await facade.load(detected);
    } else {
      // On SSR: prefer ?lang query param, then Accept-Language, fall back to SERVER_ACTIVE_LANG
      const urlLang = request ? (new URL(request.url).searchParams.get('lang') as Lang | null) : null;
      const chosenLang: Lang = (() => {
        if (urlLang && (LANG_VALUES as readonly string[]).includes(urlLang)) {
          return urlLang as Lang;
        }
        const acceptLang = request?.headers?.get('accept-language') ?? null;
        return pickLanguage(parseAcceptLanguage(acceptLang), LANG_VALUES, serverLang);
      })();
      facade.setLanguage(chosenLang);
      // Guard against build-time prerender where the browser dist i18n files may
      // not yet exist (route-extraction phase). At real SSR runtime, the browser
      // dist is fully built and the load will succeed, fixing the raw-key bug.
      try {
        await facade.load(chosenLang);
      } catch {
        // Prerender/build context: i18n files not yet present — silently skip.
        // This does NOT affect the runtime SSR server where files are available.
      }
    }
  };
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZard(),
    provideRouter(routes),
    provideClientHydration(withEventReplay(), withHttpTransferCacheOptions({ includePostRequests: false })),
    provideHttpClient(withFetch(), withInterceptors([authInterceptor])),
    {
      provide: API_BASE_URL,
      useFactory: (doc: Document) => resolveApiBaseUrl(doc),
      deps: [DOCUMENT],
    },
    // ---------------------------------------------------------------------------
    // i18n — Transloco + language detection
    // ---------------------------------------------------------------------------
    provideTransloco({
      config: {
        availableLangs: [...LANG_VALUES],
        defaultLang: 'en',
        fallbackLang: 'en',
        reRenderOnLangChange: true,
        prodMode: !isDevMode(),
      },
      loader: TranslocoTransferStateLoader,
    }),
    {
      provide: LanguageStoragePort,
      useClass: LocalStorageLanguageAdapter,
    },
    I18nFacade,
    {
      provide: APP_INITIALIZER,
      useFactory: (
        facade: I18nFacade,
        platformId: object,
        storage: LanguageStoragePort,
        serverLang: Lang,
        request: Request | null,
      ) => i18nInitializer(facade, platformId, storage, serverLang, request),
      deps: [I18nFacade, PLATFORM_ID, LanguageStoragePort, SERVER_ACTIVE_LANG, REQUEST],
      multi: true,
    },
    {
      provide: TeamContextStoragePort,
      useClass: LocalStorageTeamContextAdapter,
    },
    {
      provide: InstalledPluginsStoragePort,
      useClass: LocalStorageInstalledPluginsAdapter,
    },
    {
      provide: TelemetryPreferencePort,
      useClass: LocalStorageTelemetryPreferenceAdapter,
    },
    {
      provide: CryptoPort,
      useClass: WebCryptoAdapter,
    },
    {
      provide: AnonIdPort,
      useFactory: () => new AnonIdAdapter(inject(CryptoPort)),
    },
    {
      provide: CatalogLatestVersionPort,
      useClass: CatalogLatestVersionHttpAdapter,
    },
    {
      provide: CatalogPort,
      useClass: CatalogHttpAdapter,
    },
    {
      provide: SearchPort,
      useClass: SearchHttpAdapter,
    },
    DashboardFacade,
    CatalogFacade,
    SearchFacade,
    TelemetryFacade,
    {
      provide: DocsPort,
      useClass: DocsHttpAdapter,
    },
    DocsFacade,
    // ---------------------------------------------------------------------------
    // Auth
    // ---------------------------------------------------------------------------
    {
      provide: AuthPort,
      useClass: AuthHttpAdapter,
    },
    AuthFacade,
    {
      provide: APP_INITIALIZER,
      useFactory: (facade: AuthFacade) => silentRefreshInitializer(facade),
      deps: [AuthFacade],
      multi: true,
    },
    // ---------------------------------------------------------------------------
    // Organizations
    // ---------------------------------------------------------------------------
    {
      provide: OrgPort,
      useClass: OrgHttpAdapter,
    },
    OrganizationsFacade,
    OrgContextFacade,
    // ---------------------------------------------------------------------------
    // Device Activation
    // ---------------------------------------------------------------------------
    {
      provide: DeviceActivationPort,
      useClass: DeviceActivationHttpAdapter,
    },
    DeviceActivationFacade,
    // ---------------------------------------------------------------------------
    // Home Metrics
    // ---------------------------------------------------------------------------
    {
      provide: MarketplaceStatsPort,
      useClass: MarketplaceStatsHttpAdapter,
    },
    HomeMetricsFacade,
  ],
};

// Re-export inject for convenience so tests can override the token.
export { inject };
