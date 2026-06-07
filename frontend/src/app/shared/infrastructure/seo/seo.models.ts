export interface SeoConfig {
  readonly title: string;
  readonly description: string;
  readonly keywords?: string;
  readonly canonicalUrl?: string;
  readonly ogTitle?: string;
  readonly ogDescription?: string;
  readonly ogType?: string;
  readonly ogUrl?: string;
  readonly ogImage?: string;
  readonly twitterCard?: 'summary' | 'summary_large_image' | 'app' | 'player';
  readonly twitterTitle?: string;
  readonly twitterDescription?: string;
  readonly twitterImage?: string;
}
