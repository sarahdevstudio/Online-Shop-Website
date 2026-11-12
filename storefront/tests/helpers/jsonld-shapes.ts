/**
 * A small validator for the schema.org shapes the shop emits (Product/Offer, BreadcrumbList, FAQPage, BlogPosting, Organization,
 * WebSite, LocalBusiness, AggregateRating, Review). It checks what Google's rich-result requirements say is required or must be
 * well-formed, not the whole vocabulary: a missing required property, a wrong type, a bad URL, an invalid currency, an
 * out-of-range rating. Returns a list of problems (empty = valid).
 */
type Json = Record<string, unknown>;

const isObject = (v: unknown): v is Json => typeof v === 'object' && v !== null && !Array.isArray(v);
const isString = (v: unknown): v is string => typeof v === 'string' && v.trim().length > 0;
const isUrl = (v: unknown) => isString(v) && /^https?:\/\/[^\s]+$/.test(v);
const isDate = (v: unknown) => isString(v) && !Number.isNaN(Date.parse(v)) && /^\d{4}-\d{2}-\d{2}/.test(v);
const asArray = (v: unknown): unknown[] => (Array.isArray(v) ? v : v === undefined ? [] : [v]);

export const SCHEMA_AVAILABILITY = ['InStock', 'OutOfStock', 'Discontinued', 'PreOrder', 'LimitedAvailability', 'SoldOut', 'BackOrder'];

type Check = (node: Json, at: string, problems: string[]) => void;

function required(node: Json, keys: string[], at: string, problems: string[]) {
  for (const key of keys) if (node[key] === undefined || node[key] === null || node[key] === '') problems.push(`${at}: "${key}" is required`);
}

function typed(node: Json, at: string, problems: string[], expected: string) {
  if (node['@type'] !== expected) problems.push(`${at}: @type should be ${expected}, is ${String(node['@type'])}`);
}

const checks: Record<string, Check> = {
  Offer(node, at, problems) {
    required(node, ['price', 'priceCurrency', 'availability'], at, problems);
    if (node.price !== undefined && !(typeof node.price === 'number' ? node.price >= 0 : /^\d+(\.\d+)?$/.test(String(node.price)))) problems.push(`${at}: price must be a non-negative number`);
    if (node.priceCurrency !== undefined && !/^[A-Z]{3}$/.test(String(node.priceCurrency))) problems.push(`${at}: priceCurrency must be an ISO 4217 code`);
    const availability = String(node.availability ?? '');
    if (node.availability !== undefined && !SCHEMA_AVAILABILITY.some((a) => availability === `https://schema.org/${a}`)) problems.push(`${at}: availability must be a schema.org ItemAvailability URL`);
    if (node.url !== undefined && !isUrl(node.url)) problems.push(`${at}: url must be absolute`);
  },
  Brand(node, at, problems) {
    required(node, ['name'], at, problems);
  },
  AggregateRating(node, at, problems) {
    required(node, ['ratingValue'], at, problems);
    const value = Number(node.ratingValue);
    if (!(value >= 1 && value <= 5)) problems.push(`${at}: ratingValue must be between 1 and 5`);
    const count = Number(node.reviewCount ?? node.ratingCount);
    if (!Number.isInteger(count) || count < 1) problems.push(`${at}: reviewCount (or ratingCount) must be a positive integer`);
    if (node.bestRating !== undefined && Number(node.bestRating) !== 5) problems.push(`${at}: bestRating must be 5`);
  },
  Rating(node, at, problems) {
    required(node, ['ratingValue'], at, problems);
    if (!(Number(node.ratingValue) >= 1 && Number(node.ratingValue) <= 5)) problems.push(`${at}: ratingValue must be between 1 and 5`);
  },
  Review(node, at, problems) {
    required(node, ['author', 'reviewRating'], at, problems);
    const author = node.author;
    if (author !== undefined && !(isString(author) || (isObject(author) && author['@type'] === 'Person' && isString(author.name)))) problems.push(`${at}: author must be a name or a Person with a name`);
    if (isObject(node.reviewRating)) validateNode(node.reviewRating, `${at}.reviewRating`, problems, 'Rating');
    if (node.datePublished !== undefined && !isDate(node.datePublished)) problems.push(`${at}: datePublished must be an ISO date`);
    if (node.reviewBody !== undefined && !isString(node.reviewBody)) problems.push(`${at}: reviewBody must be text`);
  },
  Product(node, at, problems) {
    required(node, ['name'], at, problems);
    if (node.image !== undefined && !asArray(node.image).every(isUrl)) problems.push(`${at}: image must be absolute URLs`);
    if (node.offers === undefined && node.aggregateRating === undefined && node.review === undefined) problems.push(`${at}: needs offers, aggregateRating or review`);
    for (const [i, offer] of asArray(node.offers).entries()) {
      if (isObject(offer)) validateNode(offer, `${at}.offers[${i}]`, problems, 'Offer');
      else problems.push(`${at}.offers[${i}] is not an object`);
    }
    if (node.brand !== undefined && isObject(node.brand)) validateNode(node.brand, `${at}.brand`, problems, 'Brand');
    if (node.aggregateRating !== undefined && isObject(node.aggregateRating)) validateNode(node.aggregateRating, `${at}.aggregateRating`, problems, 'AggregateRating');
    for (const [i, review] of asArray(node.review).entries()) if (isObject(review)) validateNode(review, `${at}.review[${i}]`, problems, 'Review');
  },
  BreadcrumbList(node, at, problems) {
    const items = asArray(node.itemListElement);
    if (items.length === 0) problems.push(`${at}: itemListElement is empty`);
    items.forEach((item, index) => {
      const here = `${at}.itemListElement[${index}]`;
      if (!isObject(item)) return void problems.push(`${here} is not an object`);
      typed(item, here, problems, 'ListItem');
      required(item, ['position', 'name'], here, problems);
      if (item.position !== index + 1) problems.push(`${here}: position should be ${index + 1}`);
      if (item.item !== undefined && !isUrl(item.item)) problems.push(`${here}: item must be an absolute URL`);
      if (item.item === undefined && index !== items.length - 1) problems.push(`${here}: only the last item may omit "item"`);
    });
  },
  FAQPage(node, at, problems) {
    const questions = asArray(node.mainEntity);
    if (questions.length === 0) problems.push(`${at}: mainEntity is empty`);
    questions.forEach((q, index) => {
      const here = `${at}.mainEntity[${index}]`;
      if (!isObject(q)) return void problems.push(`${here} is not an object`);
      typed(q, here, problems, 'Question');
      required(q, ['name'], here, problems);
      const answer = q.acceptedAnswer;
      if (!isObject(answer)) return void problems.push(`${here}: acceptedAnswer is required`);
      typed(answer, `${here}.acceptedAnswer`, problems, 'Answer');
      required(answer, ['text'], `${here}.acceptedAnswer`, problems);
    });
  },
  BlogPosting(node, at, problems) {
    required(node, ['headline', 'datePublished'], at, problems);
    if (isString(node.headline) && node.headline.length > 110) problems.push(`${at}: headline is longer than 110 characters`);
    for (const key of ['datePublished', 'dateModified']) if (node[key] !== undefined && !isDate(node[key])) problems.push(`${at}: ${key} must be an ISO date`);
    if (node.image !== undefined && !asArray(node.image).every(isUrl)) problems.push(`${at}: image must be absolute URLs`);
    if (node.mainEntityOfPage !== undefined && !isUrl(node.mainEntityOfPage)) problems.push(`${at}: mainEntityOfPage must be an absolute URL`);
    if (node.publisher !== undefined && !(isObject(node.publisher) && isString(node.publisher.name))) problems.push(`${at}: publisher needs a name`);
  },
  Organization(node, at, problems) {
    required(node, ['name', 'url'], at, problems);
    if (node.url !== undefined && !isUrl(node.url)) problems.push(`${at}: url must be absolute`);
    if (node.logo !== undefined && !isUrl(node.logo)) problems.push(`${at}: logo must be an absolute URL`);
    for (const link of asArray(node.sameAs)) if (!isUrl(link)) problems.push(`${at}: sameAs must be absolute URLs`);
  },
  LocalBusiness(node, at, problems) {
    required(node, ['name', 'address'], at, problems);
    if (isObject(node.address)) required(node.address, ['streetAddress'], `${at}.address`, problems);
  },
  WebSite(node, at, problems) {
    required(node, ['name', 'url'], at, problems);
    const action = node.potentialAction;
    if (isObject(action)) {
      typed(action, `${at}.potentialAction`, problems, 'SearchAction');
      if (!String(action.target ?? '').includes('{search_term_string}')) problems.push(`${at}.potentialAction: target needs {search_term_string}`);
      if (action['query-input'] !== 'required name=search_term_string') problems.push(`${at}.potentialAction: query-input is wrong`);
    }
  },
};

function validateNode(node: Json, at: string, problems: string[], expectedType?: string) {
  const type = String(node['@type'] ?? '');
  if (expectedType && type !== expectedType) problems.push(`${at}: @type should be ${expectedType}, is ${type || 'missing'}`);
  const check = checks[type];
  if (!check) {
    problems.push(`${at}: no validator for @type "${type}"`);
    return;
  }
  check(node, at, problems);
}

/** Problems with one JSON-LD document (an object with @context and @type); empty when it is valid. */
export function validateJsonLd(document: unknown, at = 'jsonld'): string[] {
  const problems: string[] = [];
  if (!isObject(document)) return [`${at}: is not an object`];
  if (document['@context'] !== 'https://schema.org') problems.push(`${at}: @context must be https://schema.org`);
  validateNode(document, at, problems);
  return problems;
}
