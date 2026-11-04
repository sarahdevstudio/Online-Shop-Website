import type { Metadata } from 'next';
import { notFound, permanentRedirect } from 'next/navigation';
import { isInfoSlug } from '@/lib/content';
import type { SearchParams } from '@/lib/paging';
import { pathFromSegments, redirectUrl, resolvePath, type Resolution } from '@/lib/routes';
import { BlogPostView, blogPostMetadata } from '../_views/BlogPostView';
import { CategoryView, categoryMetadata } from '../_views/CategoryView';
import { InfoPageView, infoPageMetadata } from '../_views/InfoPageView';
import { ProductView, productMetadata } from '../_views/ProductView';

type Props = { params: Promise<{ path: string[] }>; searchParams: Promise<SearchParams> };

/**
 * The one public route for everything the admin can name: categories, products, info pages and blog posts. Static routes
 * (/cart, /search, /order/…, /admin, /blog …) win in Next before this is reached; the API's route table decides the rest
 * (docs/11 C.4). An old path never renders: it redirects (308, one hop, query string kept) to the current path.
 */
async function resolve({ params }: Props): Promise<Resolution> {
  return resolvePath(pathFromSegments((await params).path));
}

export async function generateMetadata(props: Props): Promise<Metadata> {
  const resolution = await resolve(props);
  const searchParams = await props.searchParams;
  switch (resolution.kind) {
    case 'category':
      return categoryMetadata({ slug: resolution.slug, path: resolution.path, searchParams });
    case 'product':
      return productMetadata({ slug: resolution.slug, searchParams });
    case 'contentPage':
      return isInfoSlug(resolution.slug) ? infoPageMetadata({ slug: resolution.slug }) : {};
    case 'blogPost':
      return blogPostMetadata({ slug: resolution.slug });
    default:
      return {};
  }
}

export default async function PublicPathPage(props: Props) {
  const resolution = await resolve(props);
  switch (resolution.kind) {
    case 'redirect':
      return permanentRedirect(redirectUrl(resolution.to, await props.searchParams));
    case 'category':
      return <CategoryView slug={resolution.slug} path={resolution.path} searchParams={await props.searchParams} />;
    case 'product':
      return <ProductView slug={resolution.slug} searchParams={await props.searchParams} />;
    case 'contentPage':
      return isInfoSlug(resolution.slug) ? <InfoPageView slug={resolution.slug} /> : notFound();
    case 'blogPost':
      return <BlogPostView slug={resolution.slug} />;
    default:
      // 'notFound', and 'gone' only when the proxy could not decide (it answers 410 itself): 404 is the safe answer.
      return notFound();
  }
}
