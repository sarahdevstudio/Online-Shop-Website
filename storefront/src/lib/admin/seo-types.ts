/** Shapes of `/api/v1/admin/seo/{type}/{id}` (docs/17). `rowVersion` is null until the first save; a save with an old one is a 409. */
export type SeoTargetType = 'product' | 'category' | 'contentPage' | 'blogPost';

export interface AdminSeo {
  type: SeoTargetType;
  id: number;
  name: string;
  path: string;
  seo: { metaTitle: string | null; metaDescription: string | null; ogMediaId: number | null; ogImageUrl: string | null; noIndex: boolean };
  faq: { question: string; answer: string }[];
  intro: string | null;
  supportsFaq: boolean;
  supportsIntro: boolean;
  rowVersion: string | null;
}

export const TITLE_IDEAL = 60;
export const DESCRIPTION_IDEAL = 155;
