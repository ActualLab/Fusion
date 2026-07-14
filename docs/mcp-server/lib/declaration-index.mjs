// A pragmatic, single-pass C# declaration scanner. It is comment/string/brace
// aware and emits one record per declaration with its line region, so an AI can
// jump straight to a symbol via source_read(file, startLine, endLine).
//
// It is heuristic, not a full C# parser: it recognizes namespaces, types
// (class/struct/interface/record/enum/delegate), members with a `{...}` body
// (methods, ctors, properties, events, operators, indexers) and `;`-terminated
// members declared directly in a type body (fields, auto/expression-bodied
// members, enum members). Locals inside method bodies are intentionally skipped.

const TYPE_KEYWORDS = new Set(["class", "struct", "interface", "record", "enum", "delegate"]);
const STATEMENT_KEYWORDS = /\b(if|for|foreach|while|switch|catch|using|lock|fixed|return|throw|await|yield|else|break|continue|goto|do)\b/;

function buildLineStarts(text) {
  const starts = [0];
  for (let k = 0; k < text.length; k++)
    if (text[k] === "\n")
      starts.push(k + 1);
  return starts;
}

function lineOf(lineStarts, index) {
  let lo = 0;
  let hi = lineStarts.length - 1;
  while (lo < hi) {
    const mid = (lo + hi + 1) >> 1;
    if (lineStarts[mid] <= index) lo = mid;
    else hi = mid - 1;
  }
  return lo + 1;
}

function skipString(text, i) {
  let verbatim = false;
  for (let j = i - 1; j >= 0 && (text[j] === "@" || text[j] === "$"); j--)
    if (text[j] === "@")
      verbatim = true;
  i++;
  while (i < text.length) {
    const ch = text[i];
    if (verbatim) {
      if (ch === '"') {
        if (text[i + 1] === '"') { i += 2; continue; }
        return i + 1;
      }
      i++;
    }
    else {
      if (ch === "\\") { i += 2; continue; }
      if (ch === '"') return i + 1;
      if (ch === "\n") return i;
      i++;
    }
  }
  return i;
}

function skipRawString(text, i, quoteCount) {
  const closing = '"'.repeat(quoteCount);
  const end = text.indexOf(closing, i + quoteCount);
  return end < 0 ? text.length : end + quoteCount;
}

function normalize(signature) {
  return signature
    .replace(/\/\/[^\n]*/g, " ")
    .replace(/\/\*[\s\S]*?\*\//g, " ")
    .replace(/\[[^\]]*\]/g, " ")
    .replace(/\s+/g, " ")
    .trim();
}

function extractDeclaration(signature) {
  const text = normalize(signature);
  if (!text)
    return null;

  const typeMatch = text.match(/\b(class|struct|interface|record|enum|delegate)\s+([A-Za-z_]\w*)/);
  if (typeMatch)
    return { kind: typeMatch[1], name: typeMatch[2], isType: TYPE_KEYWORDS.has(typeMatch[1]) };
  const namespaceMatch = text.match(/\bnamespace\s+([A-Za-z_][\w.]*)/);
  if (namespaceMatch)
    return { kind: "namespace", name: namespaceMatch[1], isType: false };

  const method = [...text.matchAll(/([A-Za-z_]\w*)\s*(?:<[^>]*>)?\s*\(/g)].pop();
  if (method) {
    const before = text.slice(0, method.index);
    if (!STATEMENT_KEYWORDS.test(` ${before} `) && !/=>\s*$/.test(before)) {
      if (/\bthis\s*$/.test(before))
        return { kind: "indexer", name: "this[]", isType: false };
      if (/\boperator\s*$/.test(before))
        return { kind: "operator", name: `operator ${method[1]}`, isType: false };
      return { kind: "member", name: method[1], isType: false };
    }
  }

  const trailing = text.match(/([A-Za-z_]\w*)\s*$/);
  if (trailing && !/[)}\];]$/.test(text) && !STATEMENT_KEYWORDS.test(` ${text} `))
    return { kind: "member", name: trailing[1], isType: false };
  return null;
}

function extractFieldNames(signature) {
  let text = normalize(signature).replace(/=.*$/, "").trim();
  if (!text || /[(){}<>]/.test(text) || STATEMENT_KEYWORDS.test(` ${text} `))
    return [];
  const tokens = text.split(/\s+/);
  if (tokens.length < 2)
    return [];
  const names = [];
  const declarators = text.split(",");
  for (let k = 0; k < declarators.length; k++) {
    const part = declarators[k].trim();
    const match = k === 0 ? part.match(/([A-Za-z_]\w*)\s*$/) : part.match(/^([A-Za-z_]\w*)$/);
    if (match)
      names.push(match[1]);
  }
  return names;
}

export function scanDeclarations(text, filePath) {
  const lineStarts = buildLineStarts(text);
  const decls = [];
  const frames = [];
  let chunkStart = 0;
  let i = 0;

  const emit = (name, kind, startIndex, endIndex) =>
    decls.push({ name, kind, path: filePath, startLine: lineOf(lineStarts, startIndex), endLine: lineOf(lineStarts, endIndex) });
  const directTypeFrame = () => frames.length > 0 && frames[frames.length - 1].isTypeBody;

  while (i < text.length) {
    const ch = text[i];
    if (ch === "/" && text[i + 1] === "/") { const nl = text.indexOf("\n", i); i = nl < 0 ? text.length : nl; continue; }
    if (ch === "/" && text[i + 1] === "*") { const end = text.indexOf("*/", i + 2); i = end < 0 ? text.length : end + 2; continue; }
    if (ch === "'") { i++; while (i < text.length && text[i] !== "'") { if (text[i] === "\\") i++; i++; } i++; continue; }
    if (ch === '"') {
      let quotes = 0;
      while (text[i + quotes] === '"') quotes++;
      i = quotes >= 3 ? skipRawString(text, i, quotes) : skipString(text, i);
      continue;
    }
    if (ch === "=" && text[i + 1] === ">") { i += 2; continue; }

    if (ch === "{") {
      const signature = text.slice(chunkStart, i);
      const decl = extractDeclaration(signature);
      const sigStart = chunkStart + (signature.length - signature.trimStart().length);
      if (decl && decl.kind === "namespace")
        frames.push({ isTypeBody: false, decl: null, startIndex: sigStart });
      else if (decl && decl.isType)
        frames.push({ isTypeBody: true, decl, startIndex: sigStart });
      else if (decl && directTypeFrame())
        frames.push({ isTypeBody: false, decl, startIndex: sigStart });
      else
        frames.push({ isTypeBody: false, decl: null, startIndex: sigStart });
      i++; chunkStart = i; continue;
    }
    if (ch === "}") {
      const frame = frames.pop();
      if (frame && frame.decl)
        emit(frame.decl.name, frame.decl.kind, frame.startIndex, i);
      i++; chunkStart = i; continue;
    }
    if (ch === ";") {
      if (directTypeFrame()) {
        const signature = text.slice(chunkStart, i);
        const sigStart = chunkStart + (signature.length - signature.trimStart().length);
        const decl = extractDeclaration(signature);
        if (decl && (decl.isType || decl.kind === "delegate"))
          emit(decl.name, decl.kind, sigStart, i);
        else
          for (const name of extractFieldNames(signature))
            emit(name, "field", sigStart, i);
      }
      i++; chunkStart = i; continue;
    }
    i++;
  }
  return decls;
}
