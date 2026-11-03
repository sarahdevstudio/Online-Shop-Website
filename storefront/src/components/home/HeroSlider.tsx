'use client';

import Image from 'next/image';
import Link from 'next/link';
import { useEffect, useState } from 'react';
import { messages } from '@/lib/messages';
import { formatNumber } from '@/lib/format';
import type { HeroSlide } from '@/lib/site-config';
import styles from './HeroSlider.module.css';

interface HeroSliderProps {
  slides: readonly HeroSlide[];
  autoplaySeconds: number;
}

function SlideLink({ href, children }: { href: string; children: string }) {
  const external = /^https:/i.test(href);
  return external ? (
    <a className={styles.cta} href={href} rel="noopener noreferrer nofollow" target="_blank">
      {children}
    </a>
  ) : (
    <Link className={styles.cta} href={href}>
      {children}
    </Link>
  );
}

/**
 * The home hero. Only the first slide's image is loaded with priority. Autoplay (0 = off) never runs for visitors who prefer
 * reduced motion, pauses while the pointer or focus is inside, and can be paused with a button (WCAG 2.2.2).
 */
export function HeroSlider({ slides, autoplaySeconds }: HeroSliderProps) {
  const [index, setIndex] = useState(0);
  const [paused, setPaused] = useState(false);
  const [reduced, setReduced] = useState(true);
  const [hover, setHover] = useState(false);

  const many = slides.length > 1;
  const autoplay = many && autoplaySeconds >= 4 && !reduced;

  useEffect(() => {
    const query = window.matchMedia('(prefers-reduced-motion: reduce)');
    setReduced(query.matches);
    const onChange = () => setReduced(query.matches);
    query.addEventListener('change', onChange);
    return () => query.removeEventListener('change', onChange);
  }, []);

  useEffect(() => {
    if (!autoplay || paused || hover) return;
    const timer = window.setInterval(() => setIndex((i) => (i + 1) % slides.length), autoplaySeconds * 1000);
    return () => window.clearInterval(timer);
  }, [autoplay, paused, hover, autoplaySeconds, slides.length]);

  const go = (next: number) => setIndex((next + slides.length) % slides.length);

  return (
    <section
      className={styles.hero}
      aria-roledescription="carousel"
      aria-label={messages.heroLabel}
      onMouseEnter={() => setHover(true)}
      onMouseLeave={() => setHover(false)}
      onFocus={() => setHover(true)}
      onBlur={() => setHover(false)}
    >
      <div className={styles.viewport} aria-live={autoplay && !paused && !hover ? 'off' : 'polite'}>
        {slides.map((slide, i) => (
          <div
            key={i}
            className={`${styles.slide} ${slide.image ? styles.withImage : styles.textOnly}`}
            role="group"
            aria-roledescription="slide"
            aria-label={messages.slideOf(formatNumber(i + 1), formatNumber(slides.length))}
            hidden={i !== index}
          >
            {slide.image && (
              <Image className={styles.image} src={slide.image.url} alt={slide.alt} fill sizes="100vw" priority={i === 0} loading={i === 0 ? undefined : 'lazy'} />
            )}
            {(slide.title || slide.text || (slide.link && slide.linkLabel)) && (
              <div className={`ui-container ${styles.caption}`}>
                <div className={styles.captionBox}>
                  {slide.title && <h2 className={styles.title}>{slide.title}</h2>}
                  {slide.text && <p className={styles.text}>{slide.text}</p>}
                  {slide.link && slide.linkLabel && <SlideLink href={slide.link}>{slide.linkLabel}</SlideLink>}
                </div>
              </div>
            )}
          </div>
        ))}
      </div>

      {many && (
        <div className={styles.controls}>
          <button type="button" className={`${styles.arrow} ${styles.prev}`} aria-label={messages.heroPrev} onClick={() => go(index - 1)} />
          <ul className={styles.dots}>
            {slides.map((_, i) => (
              <li key={i}>
                <button
                  type="button"
                  className={styles.dot}
                  aria-label={messages.slideOf(formatNumber(i + 1), formatNumber(slides.length))}
                  aria-current={i === index ? 'true' : undefined}
                  onClick={() => setIndex(i)}
                />
              </li>
            ))}
          </ul>
          <button type="button" className={`${styles.arrow} ${styles.next}`} aria-label={messages.heroNext} onClick={() => go(index + 1)} />
          {autoplay && (
            <button type="button" className={styles.pause} aria-pressed={paused} onClick={() => setPaused((p) => !p)}>
              {paused ? messages.heroPlay : messages.heroPause}
            </button>
          )}
        </div>
      )}
    </section>
  );
}
