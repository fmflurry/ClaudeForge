/**
 * RED tests — StructuredDataService (Group 3: SEO metadata + structured-data services)
 *
 * Expected production files (DO NOT exist yet — tests will FAIL to compile/resolve):
 *   src/app/shared/infrastructure/seo/structured-data.service.ts
 *
 * The SeoConfig model lives in seo.models.ts (same directory, written for seo-metadata service).
 *
 * GREEN contract — exact types/classes the coder MUST implement:
 *
 *   // structured-data.service.ts
 *   @Injectable({ providedIn: 'root' })
 *   export class StructuredDataService {
 *     constructor(
 *       @Inject(DOCUMENT) private readonly document: Document,
 *     ) {}
 *
 *     /**
 *      * Injects or replaces a JSON-LD <script type="application/ld+json"> containing:
 *      *   - Organization schema (name, url, logo, sameAs)
 *      *   - WebSite schema (name, url, searchAction)
 *      * Uses injected DOCUMENT — safe for SSR.
 *      * Idempotent: calling again replaces the existing script, does NOT add a duplicate.
 *      *\/
 *     injectOrganizationAndWebSite(config: {
 *       readonly organizationName: string;
 *       readonly siteUrl: string;
 *       readonly logoUrl: string;
 *       readonly sameAs?: readonly string[];
 *       readonly searchActionTemplate?: string;   // e.g. 'https://example.com/search?q={search_term_string}'
 *     }): void
 *
 *     /**
 *      * Injects or replaces a JSON-LD <script type="application/ld+json"> containing
 *      * an ItemList schema built from the provided plugins.
 *      * Uses injected DOCUMENT — safe for SSR.
 *      * Idempotent: calling again replaces the existing script, does NOT add a duplicate.
 *      *\/
 *     injectPluginItemList(plugins: readonly PluginSummary[]): void
 *
 *     /**
 *      * Removes all JSON-LD script tags injected by this service.
 *      *\/
 *     removeAll(): void
 *   }
 *
 *   JSON-LD shapes expected:
 *
 *   Organization:
 *   {
 *     "@context": "https://schema.org",
 *     "@type": "Organization",
 *     "name": <organizationName>,
 *     "url": <siteUrl>,
 *     "logo": <logoUrl>,
 *     "sameAs": <sameAs array, omit if empty>
 *   }
 *
 *   WebSite:
 *   {
 *     "@context": "https://schema.org",
 *     "@type": "WebSite",
 *     "name": <organizationName>,
 *     "url": <siteUrl>,
 *     "potentialAction": {
 *       "@type": "SearchAction",
 *       "target": <searchActionTemplate>,
 *       "query-input": "required name=search_term_string"
 *     }  // omit potentialAction if searchActionTemplate not provided
 *   }
 *
 *   ItemList (from plugins: PluginSummary[]):
 *   {
 *     "@context": "https://schema.org",
 *     "@type": "ItemList",
 *     "itemListElement": plugins.map((p, i) => ({
 *       "@type": "ListItem",
 *       "position": i + 1,
 *       "@id": <siteUrl or derived URL per plugin slug>,
 *       "name": p.name,
 *       "description": p.description,
 *       "author": p.author
 *     }))
 *   }
 *
 * SSR-SAFETY: The service MUST use the injected DOCUMENT token for all DOM access.
 * NEVER use the global `document` object or `window` directly.
 *
 * IDEMPOTENCY: re-invoking the same method must NOT duplicate <script> elements.
 * The service should locate existing scripts by a data-attribute marker
 * (e.g. data-seo="org-website" and data-seo="plugin-itemlist") and replace them.
 */

import { TestBed } from '@angular/core/testing';
import { DOCUMENT } from '@angular/common';
import { StructuredDataService } from './structured-data.service';
import type { PluginSummary } from '../../../features/catalog/domain/models/catalog.models';

// ---------------------------------------------------------------------------
// Fake DOCUMENT — simulates head element with script injection/removal
// ---------------------------------------------------------------------------

interface FakeScriptElement {
  tagName: 'SCRIPT';
  type: string;
  textContent: string;
  dataset: Record<string, string>;
  getAttribute(name: string): string | null;
  setAttribute(name: string, value: string): void;
}

interface FakeHead {
  querySelectorAll(selector: string): FakeScriptElement[];
  querySelector(selector: string): FakeScriptElement | null;
  appendChild(el: FakeScriptElement): void;
  removeChild(el: FakeScriptElement): void;
}

interface FakeDocument {
  head: FakeHead;
  createElement(tagName: 'script'): FakeScriptElement;
  _scripts: FakeScriptElement[];
}

function createFakeDocument(): FakeDocument {
  const scripts: FakeScriptElement[] = [];

  function makeScriptEl(): FakeScriptElement {
    const attrs: Record<string, string> = {};
    const el: FakeScriptElement = {
      tagName: 'SCRIPT',
      type: '',
      textContent: '',
      dataset: {},
      getAttribute(name: string): string | null {
        return attrs[name] ?? null;
      },
      setAttribute(name: string, value: string): void {
        attrs[name] = value;
        // Mirror data-* attributes into dataset
        if (name.startsWith('data-')) {
          const key = name.slice(5).replace(/-([a-z])/g, (_, c: string) => c.toUpperCase());
          el.dataset[key] = value;
        }
      },
    };
    return el;
  }

  const head: FakeHead = {
    querySelectorAll(selector: string): FakeScriptElement[] {
      if (selector === 'script[type="application/ld+json"]') {
        return [...scripts];
      }
      // Support data-attribute selector, e.g. 'script[data-seo="org-website"]'
      const attrMatch = selector.match(/\[data-([^\]="]+)(?:="([^"]*)")?\]/);
      if (attrMatch) {
        const attr = `data-${attrMatch[1]}`;
        const val = attrMatch[2];
        return scripts.filter((s) => {
          const v = s.getAttribute(attr);
          return val !== undefined ? v === val : v !== null;
        });
      }
      return [];
    },
    querySelector(selector: string): FakeScriptElement | null {
      return head.querySelectorAll(selector)[0] ?? null;
    },
    appendChild(el: FakeScriptElement): void {
      scripts.push(el);
    },
    removeChild(el: FakeScriptElement): void {
      const idx = scripts.indexOf(el);
      if (idx !== -1) scripts.splice(idx, 1);
    },
  };

  return {
    head,
    createElement(_tagName: 'script'): FakeScriptElement {
      return makeScriptEl();
    },
    _scripts: scripts,
  };
}

// ---------------------------------------------------------------------------
// Fixtures
// ---------------------------------------------------------------------------

const ORG_CONFIG = {
  organizationName: 'ClaudeForge',
  siteUrl: 'https://claudeforge.example.com',
  logoUrl: 'https://claudeforge.example.com/logo.png',
  sameAs: ['https://github.com/claudeforge', 'https://twitter.com/claudeforge'] as string[],
  searchActionTemplate: 'https://claudeforge.example.com/search?q={search_term_string}',
} as const;

const PLUGIN_A: PluginSummary = {
  pluginId: 'plugin-uuid-1',
  name: 'TypeScript Linter',
  slug: 'typescript-linter',
  description: 'Lint your TypeScript code',
  author: 'AliceAuthor',
  types: ['linter'],
  languages: ['typescript'],
  useCaseTags: ['linting'],
  downloadCount: 1200,
  latestVersion: '1.0.0',
  createdAt: new Date('2024-01-01'),
  updatedAt: new Date('2024-06-01'),
};

const PLUGIN_B: PluginSummary = {
  pluginId: 'plugin-uuid-2',
  name: 'Python Formatter',
  slug: 'python-formatter',
  description: 'Format your Python code',
  author: 'BobAuthor',
  types: ['formatter'],
  languages: ['python'],
  useCaseTags: ['formatting'],
  downloadCount: 850,
  latestVersion: '2.1.0',
  createdAt: new Date('2024-02-01'),
  updatedAt: new Date('2024-07-01'),
};

const PLUGIN_C: PluginSummary = {
  pluginId: 'plugin-uuid-3',
  name: 'Go Builder',
  slug: 'go-builder',
  description: 'Build your Go projects',
  author: 'CarolAuthor',
  types: ['builder'],
  languages: ['go'],
  useCaseTags: ['build'],
  downloadCount: 500,
  latestVersion: '0.9.0',
  createdAt: new Date('2024-03-01'),
  updatedAt: new Date('2024-08-01'),
};

const SIX_PLUGINS: readonly PluginSummary[] = [
  PLUGIN_A,
  PLUGIN_B,
  PLUGIN_C,
  { ...PLUGIN_A, pluginId: 'p4', name: 'Rust Analyzer', slug: 'rust-analyzer', author: 'D' },
  { ...PLUGIN_B, pluginId: 'p5', name: 'Java Checker', slug: 'java-checker', author: 'E' },
  { ...PLUGIN_C, pluginId: 'p6', name: 'CSS Sorter', slug: 'css-sorter', author: 'F' },
];

// ---------------------------------------------------------------------------
// Test harness
// ---------------------------------------------------------------------------

interface StructuredDataHarness {
  service: StructuredDataService;
  fakeDoc: FakeDocument;
}

function setupHarness(): StructuredDataHarness {
  const fakeDoc = createFakeDocument();
  TestBed.configureTestingModule({
    providers: [StructuredDataService, { provide: DOCUMENT, useValue: fakeDoc as unknown as Document }],
  });
  return {
    service: TestBed.inject(StructuredDataService),
    fakeDoc,
  };
}

// ---------------------------------------------------------------------------
// Instantiation — SSR-safety (uses injected DOCUMENT)
// ---------------------------------------------------------------------------

describe('StructuredDataService — instantiation', () => {
  it('should be injectable with a fake DOCUMENT token', () => {
    const { service } = setupHarness();
    expect(service).toBeDefined();
    expect(service).toBeInstanceOf(StructuredDataService);
  });

  it('should expose injectOrganizationAndWebSite() method', () => {
    const { service } = setupHarness();
    expect(typeof service.injectOrganizationAndWebSite).toBe('function');
  });

  it('should expose injectPluginItemList() method', () => {
    const { service } = setupHarness();
    expect(typeof service.injectPluginItemList).toBe('function');
  });

  it('should expose removeAll() method', () => {
    const { service } = setupHarness();
    expect(typeof service.removeAll).toBe('function');
  });
});

// ---------------------------------------------------------------------------
// injectOrganizationAndWebSite — script creation
// ---------------------------------------------------------------------------

describe('StructuredDataService — injectOrganizationAndWebSite() creates scripts', () => {
  it('should inject at least one <script type="application/ld+json"> into the document head', () => {
    const { service, fakeDoc } = setupHarness();
    service.injectOrganizationAndWebSite(ORG_CONFIG);
    const ldScripts = fakeDoc._scripts.filter((s) => s.type === 'application/ld+json');
    expect(ldScripts.length).toBeGreaterThanOrEqual(1);
  });

  it('should not use the global document — only the injected DOCUMENT token', () => {
    const fakeDoc2 = createFakeDocument();
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [StructuredDataService, { provide: DOCUMENT, useValue: fakeDoc2 as unknown as Document }],
    });
    const svc = TestBed.inject(StructuredDataService) as unknown as StructuredDataService;
    svc.injectOrganizationAndWebSite(ORG_CONFIG);

    // Scripts must be in the fake, not in the real document
    expect(fakeDoc2._scripts.length).toBeGreaterThanOrEqual(1);
    const realScripts = Array.from(document.head.querySelectorAll('script[type="application/ld+json"]'));
    expect(realScripts.length).toBe(0);
  });
});

// ---------------------------------------------------------------------------
// injectOrganizationAndWebSite — Organization schema content
// ---------------------------------------------------------------------------

describe('StructuredDataService — Organization JSON-LD schema', () => {
  function getOrgScript(fakeDoc: FakeDocument): Record<string, unknown> | undefined {
    for (const s of fakeDoc._scripts) {
      if (s.type !== 'application/ld+json') continue;
      try {
        const parsed = JSON.parse(s.textContent) as unknown;
        // Handle both single object and array of objects
        const items = Array.isArray(parsed)
          ? (parsed as Record<string, unknown>[])
          : [parsed as Record<string, unknown>];
        const org = items.find((item) => (item as Record<string, unknown>)['@type'] === 'Organization');
        if (org) return org;
      } catch {
        // not valid JSON — handled separately
      }
    }
    return undefined;
  }

  it('should produce valid parseable JSON in the script textContent', () => {
    const { service, fakeDoc } = setupHarness();
    service.injectOrganizationAndWebSite(ORG_CONFIG);
    for (const s of fakeDoc._scripts) {
      if (s.type === 'application/ld+json') {
        expect(() => JSON.parse(s.textContent)).not.toThrow();
      }
    }
  });

  it('should include @context "https://schema.org" in Organization schema', () => {
    const { service, fakeDoc } = setupHarness();
    service.injectOrganizationAndWebSite(ORG_CONFIG);
    const org = getOrgScript(fakeDoc);
    expect(org?.['@context']).toBe('https://schema.org');
  });

  it('should include @type "Organization"', () => {
    const { service, fakeDoc } = setupHarness();
    service.injectOrganizationAndWebSite(ORG_CONFIG);
    const org = getOrgScript(fakeDoc);
    expect(org?.['@type']).toBe('Organization');
  });

  it('should include the organization name', () => {
    const { service, fakeDoc } = setupHarness();
    service.injectOrganizationAndWebSite(ORG_CONFIG);
    const org = getOrgScript(fakeDoc);
    expect(org?.['name']).toBe(ORG_CONFIG.organizationName);
  });

  it('should include the site url', () => {
    const { service, fakeDoc } = setupHarness();
    service.injectOrganizationAndWebSite(ORG_CONFIG);
    const org = getOrgScript(fakeDoc);
    expect(org?.['url']).toBe(ORG_CONFIG.siteUrl);
  });

  it('should include the logo url', () => {
    const { service, fakeDoc } = setupHarness();
    service.injectOrganizationAndWebSite(ORG_CONFIG);
    const org = getOrgScript(fakeDoc);
    expect(org?.['logo']).toBe(ORG_CONFIG.logoUrl);
  });

  it('should include sameAs when provided', () => {
    const { service, fakeDoc } = setupHarness();
    service.injectOrganizationAndWebSite(ORG_CONFIG);
    const org = getOrgScript(fakeDoc);
    expect(Array.isArray(org?.['sameAs'])).toBe(true);
    const sameAs = org?.['sameAs'] as string[];
    expect(sameAs).toContain('https://github.com/claudeforge');
    expect(sameAs).toContain('https://twitter.com/claudeforge');
  });
});

// ---------------------------------------------------------------------------
// injectOrganizationAndWebSite — WebSite schema content
// ---------------------------------------------------------------------------

describe('StructuredDataService — WebSite JSON-LD schema', () => {
  function getWebSiteSchema(fakeDoc: FakeDocument): Record<string, unknown> | undefined {
    for (const s of fakeDoc._scripts) {
      if (s.type !== 'application/ld+json') continue;
      try {
        const parsed = JSON.parse(s.textContent) as unknown;
        const items = Array.isArray(parsed)
          ? (parsed as Record<string, unknown>[])
          : [parsed as Record<string, unknown>];
        const site = items.find((item) => (item as Record<string, unknown>)['@type'] === 'WebSite');
        if (site) return site;
      } catch {
        // skip non-JSON
      }
    }
    return undefined;
  }

  it('should include @type "WebSite"', () => {
    const { service, fakeDoc } = setupHarness();
    service.injectOrganizationAndWebSite(ORG_CONFIG);
    const site = getWebSiteSchema(fakeDoc);
    expect(site?.['@type']).toBe('WebSite');
  });

  it('should include @context "https://schema.org" in WebSite schema', () => {
    const { service, fakeDoc } = setupHarness();
    service.injectOrganizationAndWebSite(ORG_CONFIG);
    const site = getWebSiteSchema(fakeDoc);
    expect(site?.['@context']).toBe('https://schema.org');
  });

  it('should include the site name', () => {
    const { service, fakeDoc } = setupHarness();
    service.injectOrganizationAndWebSite(ORG_CONFIG);
    const site = getWebSiteSchema(fakeDoc);
    expect(site?.['name']).toBe(ORG_CONFIG.organizationName);
  });

  it('should include the site url', () => {
    const { service, fakeDoc } = setupHarness();
    service.injectOrganizationAndWebSite(ORG_CONFIG);
    const site = getWebSiteSchema(fakeDoc);
    expect(site?.['url']).toBe(ORG_CONFIG.siteUrl);
  });

  it('should include potentialAction with SearchAction when searchActionTemplate is provided', () => {
    const { service, fakeDoc } = setupHarness();
    service.injectOrganizationAndWebSite(ORG_CONFIG);
    const site = getWebSiteSchema(fakeDoc);
    const action = site?.['potentialAction'] as Record<string, unknown> | undefined;
    expect(action?.['@type']).toBe('SearchAction');
    expect(action?.['target']).toBe(ORG_CONFIG.searchActionTemplate);
  });

  it('should omit potentialAction when searchActionTemplate is not provided', () => {
    const { service, fakeDoc } = setupHarness();
    const configNoSearch = {
      organizationName: ORG_CONFIG.organizationName,
      siteUrl: ORG_CONFIG.siteUrl,
      logoUrl: ORG_CONFIG.logoUrl,
    };
    service.injectOrganizationAndWebSite(configNoSearch);
    const site = getWebSiteSchema(fakeDoc);
    expect(site?.['potentialAction']).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// injectOrganizationAndWebSite — idempotency
// ---------------------------------------------------------------------------

describe('StructuredDataService — injectOrganizationAndWebSite() idempotency', () => {
  it('should NOT add duplicate JSON-LD scripts when called twice', () => {
    const { service, fakeDoc } = setupHarness();
    service.injectOrganizationAndWebSite(ORG_CONFIG);
    const countAfterFirst = fakeDoc._scripts.filter((s) => s.type === 'application/ld+json').length;
    service.injectOrganizationAndWebSite(ORG_CONFIG);
    const countAfterSecond = fakeDoc._scripts.filter((s) => s.type === 'application/ld+json').length;
    expect(countAfterSecond).toBe(countAfterFirst);
  });

  it('should update organization name when called again with different config', () => {
    const { service, fakeDoc } = setupHarness();
    service.injectOrganizationAndWebSite(ORG_CONFIG);
    service.injectOrganizationAndWebSite({ ...ORG_CONFIG, organizationName: 'UpdatedForge' });
    // Find org schema across all scripts
    let foundName: string | undefined;
    for (const s of fakeDoc._scripts) {
      if (s.type !== 'application/ld+json') continue;
      try {
        const parsed = JSON.parse(s.textContent) as unknown;
        const items = Array.isArray(parsed)
          ? (parsed as Record<string, unknown>[])
          : [parsed as Record<string, unknown>];
        const org = items.find((item) => (item as Record<string, unknown>)['@type'] === 'Organization');
        if (org) foundName = org['name'] as string;
      } catch {
        // skip
      }
    }
    expect(foundName).toBe('UpdatedForge');
  });
});

// ---------------------------------------------------------------------------
// injectPluginItemList — script creation and content
// ---------------------------------------------------------------------------

describe('StructuredDataService — injectPluginItemList() creates ItemList script', () => {
  function getItemListSchema(fakeDoc: FakeDocument): Record<string, unknown> | undefined {
    for (const s of fakeDoc._scripts) {
      if (s.type !== 'application/ld+json') continue;
      try {
        const parsed = JSON.parse(s.textContent) as unknown;
        const items = Array.isArray(parsed)
          ? (parsed as Record<string, unknown>[])
          : [parsed as Record<string, unknown>];
        const itemList = items.find((item) => (item as Record<string, unknown>)['@type'] === 'ItemList');
        if (itemList) return itemList;
      } catch {
        // skip
      }
    }
    return undefined;
  }

  it('should inject a <script type="application/ld+json"> for the item list', () => {
    const { service, fakeDoc } = setupHarness();
    service.injectPluginItemList(SIX_PLUGINS);
    const ldScripts = fakeDoc._scripts.filter((s) => s.type === 'application/ld+json');
    expect(ldScripts.length).toBeGreaterThanOrEqual(1);
  });

  it('should produce valid parseable JSON', () => {
    const { service, fakeDoc } = setupHarness();
    service.injectPluginItemList(SIX_PLUGINS);
    for (const s of fakeDoc._scripts) {
      if (s.type === 'application/ld+json') {
        expect(() => JSON.parse(s.textContent)).not.toThrow();
      }
    }
  });

  it('should include @context "https://schema.org"', () => {
    const { service, fakeDoc } = setupHarness();
    service.injectPluginItemList(SIX_PLUGINS);
    const itemList = getItemListSchema(fakeDoc);
    expect(itemList?.['@context']).toBe('https://schema.org');
  });

  it('should include @type "ItemList"', () => {
    const { service, fakeDoc } = setupHarness();
    service.injectPluginItemList(SIX_PLUGINS);
    const itemList = getItemListSchema(fakeDoc);
    expect(itemList?.['@type']).toBe('ItemList');
  });

  it('should include itemListElement array with one entry per plugin', () => {
    const { service, fakeDoc } = setupHarness();
    service.injectPluginItemList(SIX_PLUGINS);
    const itemList = getItemListSchema(fakeDoc);
    const elements = itemList?.['itemListElement'] as unknown[];
    expect(Array.isArray(elements)).toBe(true);
    expect(elements.length).toBe(SIX_PLUGINS.length);
  });

  it('each itemListElement should be of @type "ListItem"', () => {
    const { service, fakeDoc } = setupHarness();
    service.injectPluginItemList(SIX_PLUGINS);
    const itemList = getItemListSchema(fakeDoc);
    const elements = itemList?.['itemListElement'] as Record<string, unknown>[];
    for (const el of elements) {
      expect(el['@type']).toBe('ListItem');
    }
  });

  it('each ListItem should have a position field starting at 1', () => {
    const { service, fakeDoc } = setupHarness();
    service.injectPluginItemList(SIX_PLUGINS);
    const itemList = getItemListSchema(fakeDoc);
    const elements = itemList?.['itemListElement'] as Record<string, unknown>[];
    elements.forEach((el, idx) => {
      expect(el['position']).toBe(idx + 1);
    });
  });

  it('each ListItem should carry the plugin name', () => {
    const { service, fakeDoc } = setupHarness();
    service.injectPluginItemList([PLUGIN_A, PLUGIN_B, PLUGIN_C]);
    const itemList = getItemListSchema(fakeDoc);
    const elements = itemList?.['itemListElement'] as Record<string, unknown>[];
    expect(elements[0]['name']).toBe(PLUGIN_A.name);
    expect(elements[1]['name']).toBe(PLUGIN_B.name);
    expect(elements[2]['name']).toBe(PLUGIN_C.name);
  });

  it('each ListItem should carry the plugin description', () => {
    const { service, fakeDoc } = setupHarness();
    service.injectPluginItemList([PLUGIN_A, PLUGIN_B]);
    const itemList = getItemListSchema(fakeDoc);
    const elements = itemList?.['itemListElement'] as Record<string, unknown>[];
    expect(elements[0]['description']).toBe(PLUGIN_A.description);
    expect(elements[1]['description']).toBe(PLUGIN_B.description);
  });

  it('each ListItem should carry the plugin author', () => {
    const { service, fakeDoc } = setupHarness();
    service.injectPluginItemList([PLUGIN_A, PLUGIN_B]);
    const itemList = getItemListSchema(fakeDoc);
    const elements = itemList?.['itemListElement'] as Record<string, unknown>[];
    expect(elements[0]['author']).toBe(PLUGIN_A.author);
    expect(elements[1]['author']).toBe(PLUGIN_B.author);
  });

  it('each ListItem should have an @id field (URL or identifier)', () => {
    const { service, fakeDoc } = setupHarness();
    service.injectPluginItemList([PLUGIN_A]);
    const itemList = getItemListSchema(fakeDoc);
    const elements = itemList?.['itemListElement'] as Record<string, unknown>[];
    expect(elements[0]['@id']).toBeDefined();
    expect(typeof elements[0]['@id']).toBe('string');
    expect((elements[0]['@id'] as string).length).toBeGreaterThan(0);
  });

  it('should handle an empty plugins array without throwing', () => {
    const { service } = setupHarness();
    expect(() => service.injectPluginItemList([])).not.toThrow();
  });

  it('empty plugins array should produce an ItemList with empty itemListElement', () => {
    const { service, fakeDoc } = setupHarness();
    service.injectPluginItemList([]);
    const itemList = getItemListSchema(fakeDoc);
    if (itemList) {
      const elements = itemList['itemListElement'] as unknown[];
      expect(elements.length).toBe(0);
    } else {
      // Acceptable: service may choose not to inject when list is empty
      expect(true).toBe(true);
    }
  });

  it('should handle a single plugin', () => {
    const { service, fakeDoc } = setupHarness();
    service.injectPluginItemList([PLUGIN_A]);
    const itemList = getItemListSchema(fakeDoc);
    const elements = itemList?.['itemListElement'] as Record<string, unknown>[];
    expect(elements.length).toBe(1);
    expect(elements[0]['name']).toBe(PLUGIN_A.name);
  });
});

// ---------------------------------------------------------------------------
// injectPluginItemList — idempotency (no duplicate scripts)
// ---------------------------------------------------------------------------

describe('StructuredDataService — injectPluginItemList() idempotency', () => {
  it('should NOT add a duplicate script when called twice with same plugins', () => {
    const { service, fakeDoc } = setupHarness();
    service.injectPluginItemList(SIX_PLUGINS);
    const countFirst = fakeDoc._scripts.filter((s) => s.type === 'application/ld+json').length;
    service.injectPluginItemList(SIX_PLUGINS);
    const countSecond = fakeDoc._scripts.filter((s) => s.type === 'application/ld+json').length;
    expect(countSecond).toBe(countFirst);
  });

  it('should update the ItemList content when called with different plugins', () => {
    const { service, fakeDoc } = setupHarness();
    service.injectPluginItemList([PLUGIN_A]);
    service.injectPluginItemList([PLUGIN_A, PLUGIN_B]);

    function getElements(): Record<string, unknown>[] {
      for (const s of fakeDoc._scripts) {
        if (s.type !== 'application/ld+json') continue;
        try {
          const parsed = JSON.parse(s.textContent) as unknown;
          const items = Array.isArray(parsed)
            ? (parsed as Record<string, unknown>[])
            : [parsed as Record<string, unknown>];
          const itemList = items.find((item) => (item as Record<string, unknown>)['@type'] === 'ItemList');
          if (itemList) return itemList['itemListElement'] as Record<string, unknown>[];
        } catch {
          // skip
        }
      }
      return [];
    }

    const elements = getElements();
    expect(elements.length).toBe(2);
    expect(elements[1]['name']).toBe(PLUGIN_B.name);
  });

  it('should not use the global document — only the injected DOCUMENT token', () => {
    const fakeDoc2 = createFakeDocument();
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [StructuredDataService, { provide: DOCUMENT, useValue: fakeDoc2 as unknown as Document }],
    });
    const svc = TestBed.inject(StructuredDataService) as unknown as StructuredDataService;
    svc.injectPluginItemList(SIX_PLUGINS);

    expect(fakeDoc2._scripts.length).toBeGreaterThanOrEqual(1);
    const realScripts = Array.from(document.head.querySelectorAll('script[type="application/ld+json"]'));
    expect(realScripts.length).toBe(0);
  });
});

// ---------------------------------------------------------------------------
// removeAll
// ---------------------------------------------------------------------------

describe('StructuredDataService — removeAll()', () => {
  it('should not throw when called before any inject call', () => {
    const { service } = setupHarness();
    expect(() => service.removeAll()).not.toThrow();
  });

  it('should remove all injected JSON-LD scripts from the document head', () => {
    const { service, fakeDoc } = setupHarness();
    service.injectOrganizationAndWebSite(ORG_CONFIG);
    service.injectPluginItemList(SIX_PLUGINS);
    service.removeAll();
    const remaining = fakeDoc._scripts.filter((s) => s.type === 'application/ld+json');
    expect(remaining.length).toBe(0);
  });

  it('should not throw when called multiple times', () => {
    const { service } = setupHarness();
    service.injectOrganizationAndWebSite(ORG_CONFIG);
    expect(() => {
      service.removeAll();
      service.removeAll();
    }).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// Combined invocation — org+website and itemList coexist independently
// ---------------------------------------------------------------------------

describe('StructuredDataService — combined invocation', () => {
  it('should inject both org/website and ItemList scripts without interference', () => {
    const { service, fakeDoc } = setupHarness();
    service.injectOrganizationAndWebSite(ORG_CONFIG);
    service.injectPluginItemList(SIX_PLUGINS);
    // Both types of schema should be findable
    let foundOrg = false;
    let foundItemList = false;
    for (const s of fakeDoc._scripts) {
      if (s.type !== 'application/ld+json') continue;
      try {
        const parsed = JSON.parse(s.textContent) as unknown;
        const items = Array.isArray(parsed)
          ? (parsed as Record<string, unknown>[])
          : [parsed as Record<string, unknown>];
        if (items.some((i) => (i as Record<string, unknown>)['@type'] === 'Organization')) foundOrg = true;
        if (items.some((i) => (i as Record<string, unknown>)['@type'] === 'ItemList')) foundItemList = true;
      } catch {
        // skip
      }
    }
    expect(foundOrg).toBe(true);
    expect(foundItemList).toBe(true);
  });

  it('re-invoking itemList should not disturb org/website script', () => {
    const { service, fakeDoc } = setupHarness();
    service.injectOrganizationAndWebSite(ORG_CONFIG);
    service.injectPluginItemList([PLUGIN_A]);
    service.injectPluginItemList([PLUGIN_A, PLUGIN_B]);

    let foundOrg = false;
    for (const s of fakeDoc._scripts) {
      if (s.type !== 'application/ld+json') continue;
      try {
        const parsed = JSON.parse(s.textContent) as unknown;
        const items = Array.isArray(parsed)
          ? (parsed as Record<string, unknown>[])
          : [parsed as Record<string, unknown>];
        if (items.some((i) => (i as Record<string, unknown>)['@type'] === 'Organization')) foundOrg = true;
      } catch {
        // skip
      }
    }
    expect(foundOrg).toBe(true);
  });
});
