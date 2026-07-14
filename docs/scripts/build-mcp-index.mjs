import { readdir, readFile, mkdir, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { createMarkdownRenderer } from "vitepress";

const docsDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const outputPath = path.join(docsDir, ".generated", "mcp-index.json");
const excludedFiles = new Set([
  "AGENTS.md",
  "ExternalLinks0.md",
  "Tasks.md",
  "api-index.md",
]);
const excludedDirectories = new Set([
  ".generated",
  ".vitepress",
  "mdsource",
  "node-modules",
  "node_modules",
  "outdated",
  "plans",
  "public",
  "slides",
  "tasks",
  "to-be-used",
]);

function routeFor(relativePath) {
  const withoutExtension = relativePath.replace(/\.md$/i, "").replaceAll("\\", "/");
  if (withoutExtension === "index")
    return "";
  if (withoutExtension.endsWith("/index"))
    return withoutExtension.slice(0, -"index".length);
  return withoutExtension;
}

async function collectMarkdownFiles(directory = docsDir) {
  const files = [];
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    if (entry.isDirectory() && excludedDirectories.has(entry.name))
      continue;
    const fullPath = path.join(directory, entry.name);
    if (entry.isDirectory())
      files.push(...await collectMarkdownFiles(fullPath));
    else if (entry.isFile() && entry.name.endsWith(".md") && !excludedFiles.has(entry.name))
      files.push(fullPath);
  }
  return files.sort();
}

function parsePage(renderer, relativePath, markdown) {
  const source = markdown.replace(/^---\r?\n[\s\S]*?\r?\n---\r?\n/, "");
  const lines = source.split(/\r?\n/);
  const tokens = renderer.parse(source, {});
  const headings = [];
  for (let index = 0; index < tokens.length; index++) {
    const headingToken = tokens[index];
    if (headingToken.type !== "heading_open")
      continue;

    const inlineToken = tokens[index + 1];
    const title = inlineToken.children
      .filter(token => token.type === "text" || token.type === "code_inline")
      .map(token => token.content)
      .join("")
      .trim();
    headings.push({
      level: Number(headingToken.tag.slice(1)),
      line: headingToken.map[0],
      markdownTitle: inlineToken.content.trim(),
      slug: headingToken.attrGet("id"),
      title,
    });
  }

  const route = routeFor(relativePath);
  const anchorFor = heading => route ? `${route}#${heading.slug}` : `#${heading.slug}`;

  const parents = headings.map(() => -1);
  const openHeadings = [];
  for (let index = 0; index < headings.length; index++) {
    while (openHeadings.length && headings[openHeadings.at(-1)].level >= headings[index].level)
      openHeadings.pop();
    parents[index] = openHeadings.at(-1) ?? -1;
    openHeadings.push(index);
  }

  const hierarchy = [];
  return headings.map((heading, index) => {
    hierarchy.length = heading.level - 1;
    const breadcrumbs = hierarchy.filter(Boolean).map(item => item.title);
    hierarchy[heading.level - 1] = heading;

    const nextHeading = headings[index + 1];
    const bodyEnd = nextHeading?.line ?? lines.length;
    let expandedEnd = lines.length;
    for (let nextIndex = index + 1; nextIndex < headings.length; nextIndex++) {
      if (headings[nextIndex].level <= heading.level) {
        expandedEnd = headings[nextIndex].line;
        break;
      }
    }

    const children = headings
      .map((child, childIndex) => ({ child, childIndex }))
      .filter(item => parents[item.childIndex] === index)
      .map(item => ({
        anchor: anchorFor(item.child),
        level: item.child.level,
        markdownTitle: item.child.markdownTitle,
        title: item.child.title,
      }));

    const body = lines.slice(heading.line + 1, bodyEnd).join("\n").trim();
    const expanded = lines.slice(heading.line, expandedEnd).join("\n").trim();
    return {
      anchor: anchorFor(heading),
      body,
      breadcrumbs,
      children,
      expanded,
      level: heading.level,
      markdownTitle: heading.markdownTitle,
      route,
      title: heading.title,
    };
  });
}

const files = await collectMarkdownFiles();
const renderer = await createMarkdownRenderer(docsDir);
const sections = [];
let intro = "";
for (const file of files) {
  const relativePath = path.relative(docsDir, file).replaceAll("\\", "/");
  const markdown = await readFile(file, "utf8");
  sections.push(...parsePage(renderer, relativePath, markdown));
  if (relativePath === "mcp-intro.md")
    intro = markdown.replace(/^---\r?\n[\s\S]*?\r?\n---\r?\n/, "").trim();
}

if (!intro)
  throw new Error("mcp-intro.md was not found in the documentation corpus.");

await mkdir(path.dirname(outputPath), { recursive: true });
await writeFile(outputPath, JSON.stringify({ intro, sections }), "utf8");
console.log(`MCP index: ${sections.length} sections from ${files.length} Markdown files.`);
