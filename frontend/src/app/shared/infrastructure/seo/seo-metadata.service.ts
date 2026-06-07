import { inject, Injectable } from '@angular/core';
import { Title, Meta } from '@angular/platform-browser';
import { DOCUMENT } from '@angular/common';
import type { SeoConfig } from './seo.models';

@Injectable({ providedIn: 'root' })
export class SeoMetadataService {
  private readonly titleService = inject(Title);
  private readonly meta = inject(Meta);
  private readonly document = inject(DOCUMENT);

  setMetadata(config: SeoConfig): void {
    this.titleService.setTitle(config.title);

    this.meta.updateTag({ name: 'description', content: config.description });

    if (config.keywords !== undefined) {
      this.meta.updateTag({ name: 'keywords', content: config.keywords });
    }

    if (config.ogTitle !== undefined) {
      this.meta.updateTag({ property: 'og:title', content: config.ogTitle });
    }
    if (config.ogDescription !== undefined) {
      this.meta.updateTag({ property: 'og:description', content: config.ogDescription });
    }
    if (config.ogType !== undefined) {
      this.meta.updateTag({ property: 'og:type', content: config.ogType });
    }
    if (config.ogUrl !== undefined) {
      this.meta.updateTag({ property: 'og:url', content: config.ogUrl });
    }
    if (config.ogImage !== undefined) {
      this.meta.updateTag({ property: 'og:image', content: config.ogImage });
    }

    if (config.twitterCard !== undefined) {
      this.meta.updateTag({ name: 'twitter:card', content: config.twitterCard });
    }
    if (config.twitterTitle !== undefined) {
      this.meta.updateTag({ name: 'twitter:title', content: config.twitterTitle });
    }
    if (config.twitterDescription !== undefined) {
      this.meta.updateTag({ name: 'twitter:description', content: config.twitterDescription });
    }
    if (config.twitterImage !== undefined) {
      this.meta.updateTag({ name: 'twitter:image', content: config.twitterImage });
    }

    if (config.canonicalUrl !== undefined) {
      const existing = this.document.head.querySelector('link[rel="canonical"]') as HTMLLinkElement | null;
      if (existing !== null) {
        existing.setAttribute('href', config.canonicalUrl);
      } else {
        const link = this.document.createElement('link');
        link.setAttribute('rel', 'canonical');
        link.setAttribute('href', config.canonicalUrl);
        this.document.head.appendChild(link);
      }
    }
  }

  clearMetadata(): void {
    this.meta.removeTag("name='description'");
    this.meta.removeTag("name='keywords'");
    this.meta.removeTag("property='og:title'");
    this.meta.removeTag("property='og:description'");
    this.meta.removeTag("property='og:type'");
    this.meta.removeTag("property='og:url'");
    this.meta.removeTag("property='og:image'");
    this.meta.removeTag("name='twitter:card'");
    this.meta.removeTag("name='twitter:title'");
    this.meta.removeTag("name='twitter:description'");
    this.meta.removeTag("name='twitter:image'");

    const canonical = this.document.head.querySelector('link[rel="canonical"]') as HTMLLinkElement | null;
    if (canonical !== null) {
      canonical.setAttribute('href', '');
    }
  }
}
