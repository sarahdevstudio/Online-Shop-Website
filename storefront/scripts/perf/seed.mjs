// Adds what the performance run needs and the development sample data lacks: one published blog post and one photo-sized
// image on the product the run measures (the sample products have no pictures, so the product page's LCP would be text).
// Idempotent.   API=http://127.0.0.1:5167 ADMIN_USER=… ADMIN_PASS=… node scripts/perf/seed.mjs
import sharp from 'sharp';
const API = (process.env.API ?? 'http://127.0.0.1:5167').replace(/\/$/, '');
const USER = process.env.ADMIN_USER ?? 'smoke-admin';
const PASS = process.env.ADMIN_PASS ?? 'Smoke-Admin-Pass-2026!';
const SLUG = 'perf-guide';

const jar = new Map();
function remember(line) {
  const [pair] = line.split(';');
  const i = pair.indexOf('=');
  jar.set(pair.slice(0, i), pair.slice(i + 1));
}

// The antiforgery cookie belongs to the token, so fetch the token and keep its cookie before every write.
async function write(method, path, body) {
  const csrfResponse = await fetch(`${API}/api/v1/auth/csrf`, { headers: { cookie: [...jar].map(([k, v]) => `${k}=${v}`).join('; ') } });
  csrfResponse.headers.getSetCookie().forEach(remember);
  const csrf = (await csrfResponse.json()).result;
  const response = await fetch(`${API}/api/v1${path}`, {
    method,
    headers: { 'content-type': 'application/json', cookie: [...jar].map(([k, v]) => `${k}=${v}`).join('; '), [csrf.headerName]: csrf.token },
    body: JSON.stringify(body),
  });
  response.headers.getSetCookie().forEach(remember);
  return response;
}

const PRODUCT = 'modern-led-wall-light-12w';

async function authorized(method, path) {
  return fetch(`${API}/api/v1${path}`, { method, headers: { cookie: [...jar].map(([k, v]) => `${k}=${v}`).join('; ') } });
}

const login = await write('POST', '/auth/login', { username: USER, password: PASS });
if (!login.ok) throw new Error(`login failed: ${login.status}`);

// ---- blog post
if ((await fetch(`${API}/api/v1/catalog/blog/${SLUG}`)).ok) {
  console.log(`post ${SLUG} already exists`);
} else {
  const paragraphs = Array.from({ length: 6 }, (_, i) => `بخش ${i + 1}: راهنمای انتخاب روشنایی مناسب برای فضاهای مختلف خانه، از دمای رنگ تا شدت نور.`);
  const created = await write('POST', '/admin/blog', {
    slug: SLUG,
    title: 'راهنمای انتخاب روشنایی',
    excerpt: 'چگونه روشنایی مناسب خانه را انتخاب کنیم.',
    body: `## راهنمای روشنایی\n\n${paragraphs.join('\n\n')}`,
    publish: true,
  });
  if (!created.ok) throw new Error(`create failed: ${created.status} ${await created.text()}`);
  console.log(`post ${SLUG} created`);
}

// ---- product image (a 1600x1600 photo-like JPEG, about what a phone camera upload looks like after cropping)
const detail = (await (await fetch(`${API}/api/v1/catalog/products/${PRODUCT}`)).json()).result;
if (detail.images.length > 0) {
  console.log(`product ${PRODUCT} already has an image`);
} else {
  const list = (await (await authorized('GET', `/admin/products?q=${PRODUCT}`)).json()).result;
  const id = (list.items ?? list).find((p) => p.slug === PRODUCT)?.id;
  if (!id) throw new Error(`product ${PRODUCT} not found in the admin list`);
  const jpeg = await sharp({ create: { width: 1600, height: 1600, channels: 3, background: '#c9b27c', noise: { type: 'gaussian', mean: 128, sigma: 28 } } }).blur(1.2).jpeg({ quality: 82 }).toBuffer();
  const form = new FormData();
  form.set('file', new Blob([jpeg], { type: 'image/jpeg' }), 'lamp.jpg');
  form.set('alt', 'چراغ دیواری');
  const csrfResponse = await fetch(`${API}/api/v1/auth/csrf`, { headers: { cookie: [...jar].map(([k, v]) => `${k}=${v}`).join('; ') } });
  csrfResponse.headers.getSetCookie().forEach(remember);
  const csrf = (await csrfResponse.json()).result;
  const uploaded = await fetch(`${API}/api/v1/admin/products/${id}/images`, {
    method: 'POST',
    headers: { cookie: [...jar].map(([k, v]) => `${k}=${v}`).join('; '), [csrf.headerName]: csrf.token },
    body: form,
  });
  if (!uploaded.ok) throw new Error(`image upload failed: ${uploaded.status} ${await uploaded.text()}`);
  console.log(`product ${PRODUCT} image uploaded (${Math.round(jpeg.length / 1024)} KB)`);
}
