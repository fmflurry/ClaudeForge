/**
 * RED tests — SeoMetadataService (Group 3: SEO metadata + structured-data services)
 *
 * Expected production files (DO NOT exist yet — tests will FAIL to compile/resolve):
 *   src/app/shared/infrastructure/seo/seo.models.ts
 *   src/app/shared/infrastructure/seo/seo-metadata.service.ts
 *
 * GREEN contract — exact types/classes the coder MUST implement:
 *
 *   // seo.models.ts
 *   export interface SeoConfig {
 *     readonly title: string;
 *     readonly description: string;
 *     readonly keywords?: string;
 *     readonly canonicalUrl?: string;
 *     readonly ogTitle?: string;
 *     readonly ogDescription?: string;
 *     readonly ogType?: string;
 *     readonly ogUrl?: string;
 *     readonly ogImage?: string;
 *     readonly twitterCard?: 'summary' | 'summary_large_image' | 'app' | 'player';
 *     readonly twitterTitle?: string;
 *     readonly twitterDescription?: string;
 *     readonly twitterImage?: string;
 *   }
 *
 *   // seo-metadata.service.ts
 *   @Injectable({ providedIn: 'root' })
 *   export class SeoMetadataService {
 *     constructor(
 *       private readonly title: Title,
 *       private readonly meta: Meta,
 *       @Inject(DOCUMENT) private readonly document: Document,
 *     ) {}
 *
 *     /**
 *      * Sets all SEO metadata from a typed SeoConfig.
 *      * - Title: Angular Title service
 *      * - description/keywords meta tags
 *      * - Open Graph: og:title, og:description, og:type, og:url, og:image
 *      * - Twitter Card: twitter:card, twitter:title, twitter:description, twitter:image
 *      * - Canonical <link rel="canonical"> via injected DOCUMENT
 *      *\/
 *     setMetadata(config: SeoConfig): void
 *
 *     /**
 *      * Removes/resets all meta tags and the canonical link
 *      * (useful for cleanup on navigation).
 *      *\/
 *     clearMetadata(): void
 *   }
 *
 * SSR-SAFETY: The service MUST use Angular's Title + Meta services and the
 * injected DOCUMENT token — never the global `document` or `window` directly.
 */

import { TestBed } from '@angular/core/testing';
import { Title, Meta } from '@angular/platform-browser';
import { DOCUMENT } from '@angular/common';
import { SeoMetadataService } from './seo-metadata.service';
import type { SeoConfig } from './seo.models';

// ---------------------------------------------------------------------------
// Helpers — minimal, isolated fakes for Angular's Title and Meta services
// ---------------------------------------------------------------------------

class FakeTitle {
  private _title = '';
  setTitle(newTitle: string): void {
    this._title = newTitle;
  }
  getTitle(): string {
    return this._title;
  }
}

class FakeMeta {
  private readonly _tags: Record<string, string> = {};

  updateTag(tag: { name?: string; property?: string; content: string }): void {
    const key = tag.name ?? tag.property ?? '';
    this._tags[key] = tag.content;
  }

  addTag(tag: { name?: string; property?: string; content: string }): void {
    const key = tag.name ?? tag.property ?? '';
    this._tags[key] = tag.content;
  }

  removeTag(selector: string): void {
    // selector is like "name='description'" or "property='og:title'"
    const match = selector.match(/(?:name|property)='([^']+)'/);
    if (match) {
      delete this._tags[match[1]];
    }
  }

  getTag(selector: string): { content: string } | null {
    const match = selector.match(/(?:name|property)='([^']+)'/);
    if (!match) return null;
    const content = this._tags[match[1]];
    return content !== undefined ? { content } : null;
  }

  /** Test helper: read back a stored tag content. */
  read(nameOrProperty: string): string | undefined {
    return this._tags[nameOrProperty];
  }
}

// ---------------------------------------------------------------------------
// Minimal fake DOCUMENT — tracks element creation against the injected token
// ---------------------------------------------------------------------------

function createFakeDocument(): Document {
  const linkElements = new Map<string, HTMLLinkElement>();

  const fakeDocument = {
    head: {
      querySelector(selector: string): Element | null {
        if (selector === 'link[rel="canonical"]') {
          return (linkElements.get('canonical') as unknown as Element) ?? null;
        }
        return null;
      },
      appendChild(el: HTMLLinkElement): void {
        const rel = el.getAttribute?.('rel') ?? '';
        linkElements.set(rel, el);
      },
      removeChild(el: HTMLLinkElement): void {
        for (const [key, val] of linkElements) {
          if (val === el) {
            linkElements.delete(key);
            break;
          }
        }
      },
    },
    createElement(tagName: string): HTMLLinkElement {
      // Return a lightweight element with attribute storage
      const attrs: Record<string, string> = {};
      return {
        tagName: tagName.toUpperCase(),
        getAttribute(name: string): string | null {
          return attrs[name] ?? null;
        },
        setAttribute(name: string, value: string): void {
          attrs[name] = value;
        },
        get rel(): string {
          return attrs['rel'] ?? '';
        },
        set rel(v: string) {
          attrs['rel'] = v;
        },
        get href(): string {
          return attrs['href'] ?? '';
        },
        set href(v: string) {
          attrs['href'] = v;
        },
      } as unknown as HTMLLinkElement;
    },
    // Expose internal map for assertions in tests
    _linkElements: linkElements,
  } as unknown as Document & { _linkElements: Map<string, HTMLLinkElement> };

  return fakeDocument;
}

// ---------------------------------------------------------------------------
// Test harness
// ---------------------------------------------------------------------------

interface SeoHarness {
  service: SeoMetadataService;
  fakeTitle: FakeTitle;
  fakeMeta: FakeMeta;
  fakeDoc: Document & { _linkElements: Map<string, HTMLLinkElement> };
}

function setupHarness(): SeoHarness {
  const fakeTitle = new FakeTitle();
  const fakeMeta = new FakeMeta();
  const fakeDoc = createFakeDocument() as Document & { _linkElements: Map<string, HTMLLinkElement> };

  TestBed.configureTestingModule({
    providers: [
      SeoMetadataService,
      { provide: Title, useValue: fakeTitle },
      { provide: Meta, useValue: fakeMeta },
      { provide: DOCUMENT, useValue: fakeDoc },
    ],
  });

  return {
    service: TestBed.inject(SeoMetadataService),
    fakeTitle,
    fakeMeta,
    fakeDoc,
  };
}

const FULL_CONFIG: SeoConfig = {
  title: 'ClaudeForge - Plugin Marketplace for Claude Code',
  description: 'Discover and install plugins for Claude Code',
  keywords: 'claude code, plugins, ai, marketplace',
  canonicalUrl: 'https://claudeforge.example.com/catalog',
  ogTitle: 'ClaudeForge Marketplace',
  ogDescription: 'Discover plugins for Claude Code',
  ogType: 'website',
  ogUrl: 'https://claudeforge.example.com',
  ogImage: 'https://claudeforge.example.com/og-image.png',
  twitterCard: 'summary_large_image',
  twitterTitle: 'ClaudeForge Marketplace',
  twitterDescription: 'Discover plugins for Claude Code',
  twitterImage: 'https://claudeforge.example.com/twitter-image.png',
};

// ---------------------------------------------------------------------------
// Service instantiation — SSR-safety assertion (uses injected DOCUMENT)
// ---------------------------------------------------------------------------

describe('SeoMetadataService — instantiation', () => {
  it('should be injectable via TestBed with fake Title, Meta, and DOCUMENT', () => {
    const { service } = setupHarness();
    expect(service).toBeDefined();
    expect(service).toBeInstanceOf(SeoMetadataService);
  });

  it('should expose a setMetadata() method', () => {
    const { service } = setupHarness();
    expect(typeof service.setMetadata).toBe('function');
  });

  it('should expose a clearMetadata() method', () => {
    const { service } = setupHarness();
    expect(typeof service.clearMetadata).toBe('function');
  });
});

// ---------------------------------------------------------------------------
// setMetadata — Angular Title service
// ---------------------------------------------------------------------------

describe('SeoMetadataService — setMetadata() sets the page title', () => {
  it('should set the page title via Angular Title service', () => {
    const { service, fakeTitle } = setupHarness();
    service.setMetadata(FULL_CONFIG);
    expect(fakeTitle.getTitle()).toBe(FULL_CONFIG.title);
  });

  it('should update the title on repeated calls', () => {
    const { service, fakeTitle } = setupHarness();
    service.setMetadata(FULL_CONFIG);
    service.setMetadata({ ...FULL_CONFIG, title: 'Updated Title' });
    expect(fakeTitle.getTitle()).toBe('Updated Title');
  });
});

// ---------------------------------------------------------------------------
// setMetadata — meta description & keywords
// ---------------------------------------------------------------------------

describe('SeoMetadataService — setMetadata() sets description and keywords', () => {
  it('should set description meta tag', () => {
    const { service, fakeMeta } = setupHarness();
    service.setMetadata(FULL_CONFIG);
    expect(fakeMeta.read('description')).toBe(FULL_CONFIG.description);
  });

  it('should set keywords meta tag when provided', () => {
    const { service, fakeMeta } = setupHarness();
    service.setMetadata(FULL_CONFIG);
    expect(fakeMeta.read('keywords')).toBe(FULL_CONFIG.keywords);
  });

  it('should not throw when keywords is omitted', () => {
    const { service } = setupHarness();
    const config: SeoConfig = { title: 'No Keywords', description: 'Some description' };
    expect(() => service.setMetadata(config)).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// setMetadata — Open Graph tags
// ---------------------------------------------------------------------------

describe('SeoMetadataService — setMetadata() sets Open Graph tags', () => {
  it('should set og:title', () => {
    const { service, fakeMeta } = setupHarness();
    service.setMetadata(FULL_CONFIG);
    expect(fakeMeta.read('og:title')).toBe(FULL_CONFIG.ogTitle);
  });

  it('should set og:description', () => {
    const { service, fakeMeta } = setupHarness();
    service.setMetadata(FULL_CONFIG);
    expect(fakeMeta.read('og:description')).toBe(FULL_CONFIG.ogDescription);
  });

  it('should set og:type', () => {
    const { service, fakeMeta } = setupHarness();
    service.setMetadata(FULL_CONFIG);
    expect(fakeMeta.read('og:type')).toBe(FULL_CONFIG.ogType);
  });

  it('should set og:url', () => {
    const { service, fakeMeta } = setupHarness();
    service.setMetadata(FULL_CONFIG);
    expect(fakeMeta.read('og:url')).toBe(FULL_CONFIG.ogUrl);
  });

  it('should set og:image', () => {
    const { service, fakeMeta } = setupHarness();
    service.setMetadata(FULL_CONFIG);
    expect(fakeMeta.read('og:image')).toBe(FULL_CONFIG.ogImage);
  });

  it('should not throw when OG fields are omitted', () => {
    const { service } = setupHarness();
    const config: SeoConfig = { title: 'Minimal', description: 'Minimal description' };
    expect(() => service.setMetadata(config)).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// setMetadata — Twitter Card tags
// ---------------------------------------------------------------------------

describe('SeoMetadataService — setMetadata() sets Twitter Card tags', () => {
  it('should set twitter:card', () => {
    const { service, fakeMeta } = setupHarness();
    service.setMetadata(FULL_CONFIG);
    expect(fakeMeta.read('twitter:card')).toBe(FULL_CONFIG.twitterCard);
  });

  it('should set twitter:title', () => {
    const { service, fakeMeta } = setupHarness();
    service.setMetadata(FULL_CONFIG);
    expect(fakeMeta.read('twitter:title')).toBe(FULL_CONFIG.twitterTitle);
  });

  it('should set twitter:description', () => {
    const { service, fakeMeta } = setupHarness();
    service.setMetadata(FULL_CONFIG);
    expect(fakeMeta.read('twitter:description')).toBe(FULL_CONFIG.twitterDescription);
  });

  it('should set twitter:image', () => {
    const { service, fakeMeta } = setupHarness();
    service.setMetadata(FULL_CONFIG);
    expect(fakeMeta.read('twitter:image')).toBe(FULL_CONFIG.twitterImage);
  });

  it('should not throw when Twitter Card fields are omitted', () => {
    const { service } = setupHarness();
    const config: SeoConfig = { title: 'Minimal', description: 'Minimal description' };
    expect(() => service.setMetadata(config)).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// setMetadata — canonical <link rel="canonical"> via injected DOCUMENT
// ---------------------------------------------------------------------------

describe('SeoMetadataService — setMetadata() manages canonical link via injected DOCUMENT', () => {
  it('should create a <link rel="canonical"> element in the document head', () => {
    const { service, fakeDoc } = setupHarness();
    service.setMetadata(FULL_CONFIG);
    const canonical = fakeDoc._linkElements.get('canonical');
    expect(canonical).toBeDefined();
  });

  it('should set the href of the canonical link to canonicalUrl', () => {
    const { service, fakeDoc } = setupHarness();
    service.setMetadata(FULL_CONFIG);
    const canonical = fakeDoc._linkElements.get('canonical');
    expect(canonical?.getAttribute('href')).toBe(FULL_CONFIG.canonicalUrl);
  });

  it('should update the canonical link href on re-invocation (not duplicate)', () => {
    const { service, fakeDoc } = setupHarness();
    service.setMetadata(FULL_CONFIG);
    service.setMetadata({ ...FULL_CONFIG, canonicalUrl: 'https://claudeforge.example.com/new' });
    // Still exactly one canonical link
    expect(fakeDoc._linkElements.size).toBeLessThanOrEqual(1);
    const canonical = fakeDoc._linkElements.get('canonical');
    expect(canonical?.getAttribute('href')).toBe('https://claudeforge.example.com/new');
  });

  it('should not create a canonical link when canonicalUrl is omitted', () => {
    const { service, fakeDoc } = setupHarness();
    service.setMetadata({ title: 'No canonical', description: 'No canonical' });
    expect(fakeDoc._linkElements.get('canonical')).toBeUndefined();
  });

  it('should NOT use the global document object — uses injected DOCUMENT token', () => {
    // If the service uses global `document`, it would bypass our fake. We verify by
    // injecting a distinct fake and confirming the canonical appears in _that_ fake.
    const fakeTitle2 = new FakeTitle();
    const fakeMeta2 = new FakeMeta();
    const fakeDoc2 = createFakeDocument() as Document & { _linkElements: Map<string, HTMLLinkElement> };

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        SeoMetadataService,
        { provide: Title, useValue: fakeTitle2 },
        { provide: Meta, useValue: fakeMeta2 },
        { provide: DOCUMENT, useValue: fakeDoc2 },
      ],
    });
    const svc = TestBed.inject(SeoMetadataService) as unknown as SeoMetadataService;
    svc.setMetadata(FULL_CONFIG);

    // canonical must appear in the fake we injected — NOT in the real DOM
    expect(fakeDoc2._linkElements.get('canonical')).toBeDefined();
    // real document.head must NOT have been touched
    const realCanonical = document.head.querySelector('link[rel="canonical"]');
    expect(realCanonical).toBeNull();
  });
});

// ---------------------------------------------------------------------------
// clearMetadata
// ---------------------------------------------------------------------------

describe('SeoMetadataService — clearMetadata()', () => {
  it('should not throw when called before setMetadata()', () => {
    const { service } = setupHarness();
    expect(() => service.clearMetadata()).not.toThrow();
  });

  it('should remove the canonical link from the document head after clear', () => {
    const { service, fakeDoc } = setupHarness();
    service.setMetadata(FULL_CONFIG);
    service.clearMetadata();
    // canonical element should be gone (either removed from map or href reset)
    const canonical = fakeDoc._linkElements.get('canonical');
    const href = canonical?.getAttribute('href') ?? '';
    expect(href).toBeFalsy();
  });
});

// ---------------------------------------------------------------------------
// SeoConfig type — no `any`, immutable readonly fields
// ---------------------------------------------------------------------------

describe('SeoConfig model — type constraints', () => {
  it('should accept a full config object with all optional fields', () => {
    const config: SeoConfig = { ...FULL_CONFIG };
    expect(config.title).toBeDefined();
    expect(config.description).toBeDefined();
  });

  it('should accept a minimal config with only title and description', () => {
    const config: SeoConfig = { title: 'T', description: 'D' };
    expect(config.title).toBe('T');
    expect(config.description).toBe('D');
  });

  it('twitterCard field should accept all four valid card types', () => {
    const cards: SeoConfig['twitterCard'][] = ['summary', 'summary_large_image', 'app', 'player'];
    cards.forEach((card) => {
      const config: SeoConfig = { title: 'T', description: 'D', twitterCard: card };
      expect(config.twitterCard).toBe(card);
    });
  });
});
