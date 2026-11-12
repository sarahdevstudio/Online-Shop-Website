// node --test tools/content-scrape/scrape.test.mjs
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { parseNewsWidget, parseEnglishDate, excerptOf } from './scrape.mjs';

test('parseEnglishDate: converts an English date to UTC ISO midnight', () => {
  assert.equal(parseEnglishDate('01 September 2019'), '2019-09-01T00:00:00Z');
  assert.equal(parseEnglishDate('28 August 2019'), '2019-08-28T00:00:00Z');
});

test('parseEnglishDate: rejects an unrecognized shape', () => {
  assert.throws(() => parseEnglishDate('September 2019'));
});

test('excerptOf: takes the first sentence', () => {
  assert.equal(excerptOf('اول. دوم.'), 'اول.');
});

test('excerptOf: falls back to a capped prefix when there is no sentence end', () => {
  const body = 'a'.repeat(400);
  const excerpt = excerptOf(body);
  assert.ok(excerpt.length <= 300);
  assert.ok(excerpt.endsWith('...'));
});

test('parseNewsWidget: maps each post path to its title, date and feature image', () => {
  const html =
    '<a href="https://ghr-co.com/post-a/"> <div class="popular-featured-img" style="background-image: url(https://ghr-co.com/wp-content/uploads/a.jpg);"></div>' +
    '<span class="meta-wrap"><span class="post-title">عنوان الف</span> <span class="post-date">01 September 2019</span></span></a>' +
    '<a href="https://ghr-co.com/post-b/"> <div class="popular-featured-img" style="background-image: url(https://ghr-co.com/wp-content/uploads/b.jpg);"></div>' +
    '<span class="meta-wrap"><span class="post-title">عنوان ب</span> <span class="post-date">28 August 2019</span></span></a>';
  assert.deepEqual(parseNewsWidget(html), {
    '/post-a/': { imageUrl: 'https://ghr-co.com/wp-content/uploads/a.jpg', title: 'عنوان الف', date: '01 September 2019' },
    '/post-b/': { imageUrl: 'https://ghr-co.com/wp-content/uploads/b.jpg', title: 'عنوان ب', date: '28 August 2019' },
  });
});
