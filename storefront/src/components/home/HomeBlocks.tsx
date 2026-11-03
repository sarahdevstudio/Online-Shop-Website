import type { HomeBlock } from '@/lib/site-config';
import { BannerBlock } from './BannerBlock';
import { CategoryTilesBlock } from './CategoryTilesBlock';
import { HeroSlider } from './HeroSlider';
import { ProductRowBlock } from './ProductRowBlock';
import { TextBlock } from './TextBlock';

/** Maps the configured blocks to their components, in order. Each block is its own server component. */
export function HomeBlocks({ blocks }: { blocks: readonly HomeBlock[] }) {
  const firstRowId = blocks.find((b) => b.type === 'productRow')?.id;
  return (
    <>
      {blocks.map((block) => {
        const headingId = `block-${block.id}`;
        switch (block.type) {
          case 'hero':
            return <HeroSlider key={block.id} slides={block.data.slides} autoplaySeconds={block.data.autoplaySeconds} />;
          case 'categoryTiles':
            return <CategoryTilesBlock key={block.id} data={block.data} headingId={headingId} />;
          case 'productRow':
            return <ProductRowBlock key={block.id} data={block.data} headingId={headingId} firstRow={block.id === firstRowId} />;
          case 'text':
            return <TextBlock key={block.id} data={block.data} headingId={headingId} />;
          case 'banner':
            return <BannerBlock key={block.id} data={block.data} />;
        }
      })}
    </>
  );
}
