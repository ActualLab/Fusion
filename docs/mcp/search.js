const DEFAULT_SITE_URL = "https://fusion.actuallab.net";
const EXPANDED_INLINE_LIMIT = 4000;

function markdownText(text) {
  const codeSpans = [];
  return text
    .replace(/`([^`]+)`/g, (_match, code) => {
      const index = codeSpans.push(code) - 1;
      return `CODE_SPAN_${index}_END`;
    })
    .replace(/!\[([^\]]*)]\([^)]*\)/g, "$1")
    .replace(/\[([^\]]+)]\([^)]*\)/g, "$1")
    .replace(/<[^>]+>/g, " ")
    .replace(/CODE_SPAN_(\d+)_END/g, (_match, index) => codeSpans[Number(index)])
    .replace(/[~*_#>|]/g, " ")
    .replace(/\s+/g, " ")
    .trim()
    .toLowerCase();
}

function clampLimit(limit, defaultValue, maximum) {
  if (!Number.isFinite(limit))
    return defaultValue;
  return Math.max(1, Math.min(maximum, Math.trunc(limit)));
}

function normalizedAnchor(value) {
  let anchor = value.trim();
  try {
    const url = new URL(anchor);
    anchor = `${url.pathname}${url.hash}`;
  }
  catch {}
  return anchor.replace(/^\/+/, "").replace(/\.html(?=#|$)/, "");
}

function sourceUrl(section, siteUrl = DEFAULT_SITE_URL) {
  return `${siteUrl.replace(/\/$/, "")}/${section.anchor}`;
}

function baseUrl(siteUrl = DEFAULT_SITE_URL) {
  return `${siteUrl.replace(/\/$/, "")}/`;
}

function scoreSection(section, phrase, words) {
  const title = section.title.toLowerCase();
  const breadcrumbs = section.breadcrumbs.join(" ").toLowerCase();
  const body = markdownText(section.body);
  const combined = `${title} ${breadcrumbs} ${body}`;
  if (!words.every(word => combined.includes(word)))
    return -1;

  let score = 0;
  if (title === phrase)
    score += 1000;
  else if (title.startsWith(phrase))
    score += 700;
  else if (title.includes(phrase))
    score += 500;
  if (body.includes(phrase))
    score += 120;
  if (breadcrumbs.includes(phrase))
    score += 80;
  for (const word of words) {
    if (title.includes(word))
      score += 60;
    if (breadcrumbs.includes(word))
      score += 15;
    if (body.includes(word))
      score += 8;
  }
  score -= section.level;
  return score;
}

export function findSections(index, query, limit, maximum = 20) {
  const phrase = query.trim().toLowerCase();
  const words = phrase.split(/\s+/).filter(Boolean);
  if (words.length === 0)
    return [];
  const count = clampLimit(limit, 10, maximum);
  return index.sections
    .map(section => ({ section, score: scoreSection(section, phrase, words) }))
    .filter(match => match.score >= 0)
    .sort((left, right) => right.score - left.score || left.section.anchor.localeCompare(right.section.anchor))
    .slice(0, count)
    .map(match => match.section);
}

export function renderSearch(index, query, limit, siteUrl) {
  const matches = findSections(index, query, limit, 20);
  if (matches.length === 0)
    return `# Search results\n\nNo documentation anchors matched **${query.trim()}**.`;

  const lines = [`# Search results for “${query.trim()}”`, ""];
  for (const section of matches) {
    const name = [...section.breadcrumbs, section.title].join(" › ");
    lines.push(`- [${name}](${sourceUrl(section, siteUrl)}) — \`${section.anchor}\``);
  }
  return lines.join("\n");
}

export function renderSection(index, anchor, siteUrl) {
  const normalized = normalizedAnchor(anchor);
  const section = index.sections.find(item => item.anchor.toLowerCase() === normalized.toLowerCase());
  if (!section)
    return `# Documentation section not found\n\nNo section has the anchor \`${normalized}\`. Use \`search\` to find a valid anchor.`;

  const header = `Source: [\`${section.anchor}\`](${sourceUrl(section, siteUrl)})\nBase URL: ${baseUrl(siteUrl)}`;
  const children = section.children ?? [];
  if (children.length === 0 || section.expanded.length <= EXPANDED_INLINE_LIMIT)
    return `${header}\n\n${section.expanded}`;

  const content = [`###### ${section.markdownTitle}`];
  if (section.body)
    content.push("", section.body);
  content.push("", "Content below is truncated to sub-headers only, use the `get` tool to fetch any of them.", "");
  for (const child of children)
    content.push(`${"#".repeat(child.level)} [${child.title}](${sourceUrl(child, siteUrl)})`);
  return `${header}\n\n${content.join("\n")}`;
}

export function renderExpandedSearch(index, query, limit, siteUrl) {
  const matches = findSections(index, query, clampLimit(limit, 5, 10), 10);
  if (matches.length === 0)
    return `# Expanded search results\n\nNo documentation anchors matched **${query.trim()}**.`;

  const blocks = [`# Expanded search results for “${query.trim()}”`, `Base URL: ${baseUrl(siteUrl)}`];
  for (const section of matches) {
    blocks.push(`Source: [\`${section.anchor}\`](${sourceUrl(section, siteUrl)})`, section.expanded);
  }
  return blocks.join("\n\n");
}

export function renderIntro(index) {
  return index.intro;
}
