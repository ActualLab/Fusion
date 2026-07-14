import { readFile } from "node:fs/promises";
import path from "node:path";
import { buildSourceIndex } from "../lib/source-index.mjs";
import { runRipgrep } from "../lib/rg.mjs";

const REPO_URL = "https://github.com/ActualLab/Fusion";
const READY_WAIT_MS = Number(process.env.SOURCE_READY_WAIT_MS ?? 15000);
const READ_LIMIT_BYTES = 64 * 1024;
const SLOW_QUERY_MS = 1000;

const startingNotice =
  `The Fusion source index is still starting — check back in ~10 seconds.\n`
  + `In the meantime, use the Fusion source in your own file system if it exists, `
  + `or clone it: ${REPO_URL}`;

function slowNotice(elapsedMs) {
  return `_(This query took ${Math.round(elapsedMs)} ms. For heavy source navigation, cloning ${REPO_URL} `
    + `and using your own tools is usually faster; this MCP is best for conceptual documentation and quick lookups.)_`;
}

function withTimeout(promise, ms) {
  return Promise.race([
    promise.then(value => ({ value })),
    new Promise(resolve => setTimeout(() => resolve({ timedOut: true }), ms)),
  ]);
}

export function createSourceTools(baseDir, roots) {
  const indexPromise = buildSourceIndex(baseDir, roots).catch(error => ({ error }));

  async function awaitIndex() {
    const result = await withTimeout(indexPromise, READY_WAIT_MS);
    if (result.timedOut || !result.value || result.value.error)
      return null;
    return result.value;
  }

  async function sourceIndex({ pattern, limit = 50 }) {
    const index = await awaitIndex();
    if (!index)
      return startingNotice;
    let regex;
    try {
      regex = new RegExp(pattern, "i");
    }
    catch (error) {
      return `Invalid regular expression: ${error.message}`;
    }
    const started = performance.now();
    const matches = index.manifest.split("\n").filter(line => regex.test(line));
    const elapsed = performance.now() - started;
    const shown = matches.slice(0, limit);
    const header = `# Source files matching /${pattern}/i (${matches.length} of ${index.files.length}${matches.length > shown.length ? `, showing ${shown.length}` : ""})`;
    const body = shown.length > 0 ? shown.join("\n") : "No files matched.";
    const parts = [header, "", body];
    if (elapsed > SLOW_QUERY_MS)
      parts.push("", slowNotice(elapsed));
    return parts.join("\n");
  }

  async function sourceSearch({ query, glob, context = 2, fixedStrings = false, ignoreCase = false }) {
    const index = await awaitIndex();
    if (!index)
      return startingNotice;
    const args = [
      "--no-heading", "--line-number", "--with-filename", "--color", "never",
      "--max-columns", "250", "--max-columns-preview", "--no-messages",
      "--context", String(Math.max(0, Math.min(5, context))),
      "-g", "*.cs", "-g", "*.razor",
    ];
    if (fixedStrings)
      args.push("--fixed-strings");
    if (ignoreCase)
      args.push("--ignore-case");
    args.push("--regexp", query, "--", ...roots);

    const result = await runRipgrep(args, { cwd: baseDir });
    if (result.busy)
      return "The source search server is busy right now — please retry in a few seconds.";

    const parts = [];
    if (result.timedOut)
      parts.push(`_(Search exceeded ${Math.round(result.elapsedMs)} ms and was stopped; results below are partial. Consider cloning ${REPO_URL}.)_`, "");
    else if (result.elapsedMs > SLOW_QUERY_MS)
      parts.push(slowNotice(result.elapsedMs), "");

    const output = result.stdout.trimEnd();
    parts.push(output.length > 0 ? "```\n" + output + "\n```" : `No matches for /${query}/ in ${roots.join(", ")}.`);
    if (result.truncated)
      parts.push("", `[Output truncated at 64 KB — narrow your query, add a glob, or clone ${REPO_URL}.]`);
    return parts.join("\n");
  }

  async function symbolSearch({ pattern, limit = 50 }) {
    const index = await awaitIndex();
    if (!index)
      return startingNotice;
    let regex;
    try {
      regex = new RegExp(pattern, "i");
    }
    catch (error) {
      return `Invalid regular expression: ${error.message}`;
    }
    const started = performance.now();
    const matches = index.symbols.split("\n").filter(line => line && regex.test(line));
    const elapsed = performance.now() - started;
    const shown = matches.slice(0, limit);
    const header = `# Declarations matching /${pattern}/i (${matches.length} of ${index.symbolCount}${matches.length > shown.length ? `, showing ${shown.length}` : ""})`;
    const legend = "Columns: name  kind  path  startLine  endLine — fetch a region with source_read(file=path, startLine, endLine).";
    const body = shown.length > 0 ? "```\n" + shown.join("\n") + "\n```" : "No declarations matched.";
    const parts = [header, "", legend, "", body];
    if (elapsed > SLOW_QUERY_MS)
      parts.push("", slowNotice(elapsed));
    return parts.join("\n");
  }

  async function sourceRead({ file, startLine, endLine }) {
    const index = await awaitIndex();
    if (!index)
      return startingNotice;
    const normalized = file.replaceAll("\\", "/").replace(/^\/+/, "");
    if (!index.byPath.has(normalized))
      return `No source file at \`${normalized}\`. Use \`source_index\` or \`source_search\` to find valid paths.`;

    const text = await readFile(path.join(baseDir, normalized), "utf8");
    const allLines = text.split("\n");
    const hasRange = Number.isFinite(startLine);
    const from = hasRange ? Math.max(1, Math.trunc(startLine)) : 1;
    const to = Number.isFinite(endLine) ? Math.min(allLines.length, Math.trunc(endLine)) : allLines.length;

    if (!hasRange && text.length > READ_LIMIT_BYTES)
      return `\`${normalized}\` is ${(text.length / 1024).toFixed(0)} KB (${allLines.length} lines). `
        + `Pass startLine/endLine to read a range (max 64 KB per call).`;

    const selected = allLines.slice(from - 1, to);
    let body = selected.join("\n");
    let note = "";
    if (Buffer.byteLength(body, "utf8") > READ_LIMIT_BYTES) {
      body = Buffer.from(body, "utf8").subarray(0, READ_LIMIT_BYTES).toString("utf8");
      note = `\n[Truncated at 64 KB — request a smaller line range.]`;
    }
    const numbered = body.split("\n").map((line, offset) => `${from + offset}\t${line}`).join("\n");
    return `\`${normalized}\` (lines ${from}-${from + selected.length - 1} of ${allLines.length})\n\n\`\`\`\n${numbered}\n\`\`\`${note}`;
  }

  return { sourceIndex, symbolSearch, sourceSearch, sourceRead, indexPromise };
}
