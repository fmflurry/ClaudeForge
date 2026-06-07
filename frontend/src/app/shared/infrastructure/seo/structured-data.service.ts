import { inject, Injectable } from '@angular/core';
import { DOCUMENT } from '@angular/common';
import type { PluginSummary } from '../../../features/catalog/domain/models/catalog.models';

interface OrganizationConfig {
  readonly organizationName: string;
  readonly siteUrl: string;
  readonly logoUrl: string;
  readonly sameAs?: readonly string[];
  readonly searchActionTemplate?: string;
}

interface SearchAction {
  readonly '@type': 'SearchAction';
  readonly target: string;
  readonly 'query-input': string;
}

interface WebSiteSchema {
  readonly '@context': 'https://schema.org';
  readonly '@type': 'WebSite';
  readonly name: string;
  readonly url: string;
  readonly potentialAction?: SearchAction;
}

interface OrganizationSchema {
  readonly '@context': 'https://schema.org';
  readonly '@type': 'Organization';
  readonly name: string;
  readonly url: string;
  readonly logo: string;
  readonly sameAs?: readonly string[];
}

interface ListItemSchema {
  readonly '@type': 'ListItem';
  readonly position: number;
  readonly '@id': string;
  readonly name: string;
  readonly description: string;
  readonly author: string;
}

interface ItemListSchema {
  readonly '@context': 'https://schema.org';
  readonly '@type': 'ItemList';
  readonly itemListElement: readonly ListItemSchema[];
}

const SEO_ATTR = 'data-seo';
const ORG_WEBSITE_MARKER = 'org-website';
const PLUGIN_ITEMLIST_MARKER = 'plugin-itemlist';
const LD_JSON_TYPE = 'application/ld+json';

@Injectable({ providedIn: 'root' })
export class StructuredDataService {
  private readonly document = inject(DOCUMENT);

  injectOrganizationAndWebSite(config: OrganizationConfig): void {
    const orgSchema: OrganizationSchema = {
      '@context': 'https://schema.org',
      '@type': 'Organization',
      name: config.organizationName,
      url: config.siteUrl,
      logo: config.logoUrl,
      ...(config.sameAs !== undefined && config.sameAs.length > 0 ? { sameAs: config.sameAs } : {}),
    };

    const websiteSchema: WebSiteSchema = {
      '@context': 'https://schema.org',
      '@type': 'WebSite',
      name: config.organizationName,
      url: config.siteUrl,
      ...(config.searchActionTemplate !== undefined
        ? {
            potentialAction: {
              '@type': 'SearchAction',
              target: config.searchActionTemplate,
              'query-input': 'required name=search_term_string',
            } satisfies SearchAction,
          }
        : {}),
    };

    const json = JSON.stringify([orgSchema, websiteSchema]);
    this.upsertScript(ORG_WEBSITE_MARKER, json);
  }

  injectPluginItemList(plugins: readonly PluginSummary[]): void {
    const itemListElement: readonly ListItemSchema[] = plugins.map((plugin, index) => ({
      '@type': 'ListItem',
      position: index + 1,
      '@id': plugin.slug,
      name: plugin.name,
      description: plugin.description,
      author: plugin.author,
    }));

    const schema: ItemListSchema = {
      '@context': 'https://schema.org',
      '@type': 'ItemList',
      itemListElement,
    };

    const json = JSON.stringify(schema);
    this.upsertScript(PLUGIN_ITEMLIST_MARKER, json);
  }

  removeAll(): void {
    const scripts = this.document.head.querySelectorAll(`script[type="${LD_JSON_TYPE}"]`);
    scripts.forEach((script) => {
      this.document.head.removeChild(script as HTMLScriptElement);
    });
  }

  private upsertScript(marker: string, json: string): void {
    // Guard: document.head may be null/undefined during SSR.
    // Fall through silently — the JSON-LD will be injected on hydration.
    if (!this.document.head) {
      return;
    }

    const selector = `script[${SEO_ATTR}="${marker}"]`;
    // querySelector may return undefined (not null) in some server-side DOM
    // implementations — use a falsy check for safety.
    const existing = this.document.head.querySelector(selector) as HTMLScriptElement | null | undefined;

    if (existing) {
      existing.textContent = json;
    } else {
      try {
        const script = this.document.createElement('script');
        script.type = LD_JSON_TYPE;
        script.setAttribute(SEO_ATTR, marker);
        script.textContent = json;
        this.document.head.appendChild(script);
      } catch {
        // SSR environment may restrict script element manipulation — skip silently.
        // The JSON-LD will be re-injected on browser hydration.
      }
    }
  }
}
