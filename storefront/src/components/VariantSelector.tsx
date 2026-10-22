import Link from 'next/link';
import type { VariantAxisDto, VariantDto } from '@/lib/catalog';
import { variantForOption, variantQuery } from '@/lib/variants';
import styles from './VariantSelector.module.css';

interface VariantSelectorProps {
  basePath: string;
  axes: readonly VariantAxisDto[];
  variants: readonly VariantDto[];
  selected: VariantDto;
}

/**
 * One group per axis (wattage, color temperature, ...). Each option is a plain link to the URL that preselects the
 * closest existing variant, so it works without JavaScript and never offers a combination that doesn't exist.
 */
export function VariantSelector({ basePath, axes, variants, selected }: VariantSelectorProps) {
  return (
    <div className={styles.selector}>
      {axes.map((axis) => (
        <fieldset key={axis.code} className={styles.axis}>
          <legend className={styles.legend}>{axis.name}</legend>
          <ul className={styles.options}>
            {axis.options.map((option) => {
              const target = variantForOption(variants, selected, axis.code, option.slug);
              if (!target) return null;
              const current = selected.attributes[axis.code] === option.slug;
              return (
                <li key={option.slug}>
                  <Link
                    href={`${basePath}${variantQuery(axes, target)}`}
                    className={`${styles.option} ${current ? styles.current : ''} ${target.inStock ? '' : styles.soldOut}`}
                    aria-current={current ? 'true' : undefined}
                    replace
                    scroll={false}
                    rel="nofollow"
                  >
                    {option.value}
                  </Link>
                </li>
              );
            })}
          </ul>
        </fieldset>
      ))}
    </div>
  );
}
