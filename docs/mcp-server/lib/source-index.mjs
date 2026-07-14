import { readdir, readFile, stat } from "node:fs/promises";
import path from "node:path";
import { scanDeclarations } from "./declaration-index.mjs";

const SOURCE_EXTENSIONS = new Set([".cs", ".razor"]);
const EXCLUDED_DIRECTORIES = new Set(["bin", "obj", "node_modules", ".git", ".vs", ".idea", "artifacts", "tmp"]);
const TYPE_DECLARATION = /(?:^|\n)\s*(?:public|internal|private|protected|sealed|abstract|static|partial|readonly|file|ref|unsafe|new|\s)*\b(?:class|struct|interface|record|enum|delegate)\s+([A-Za-z_]\w*)/g;

async function collectSourceFiles(root, base, files) {
  let entries;
  try {
    entries = await readdir(path.join(base, root), { withFileTypes: true });
  }
  catch {
    return files;
  }
  for (const entry of entries) {
    if (entry.isDirectory()) {
      if (!EXCLUDED_DIRECTORIES.has(entry.name))
        await collectSourceFiles(path.join(root, entry.name), base, files);
    }
    else if (entry.isFile() && SOURCE_EXTENSIONS.has(path.extname(entry.name).toLowerCase())) {
      files.push(path.join(root, entry.name).replaceAll("\\", "/"));
    }
  }
  return files;
}

function typeNamesOf(text) {
  const names = new Set();
  for (const match of text.matchAll(TYPE_DECLARATION))
    names.add(match[1]);
  return [...names];
}

export async function buildSourceIndex(baseDir, roots) {
  const relativePaths = [];
  for (const root of roots)
    await collectSourceFiles(root, baseDir, relativePaths);
  relativePaths.sort();

  const files = [];
  const lines = [];
  const symbolLines = [];
  let symbolCount = 0;
  for (const relativePath of relativePaths) {
    const fullPath = path.join(baseDir, relativePath);
    const info = await stat(fullPath);
    const text = await readFile(fullPath, "utf8");
    const types = typeNamesOf(text);
    files.push({ path: relativePath, size: info.size, lineCount: text.split("\n").length });
    lines.push(types.length > 0 ? `${relativePath} :: ${types.join(", ")}` : relativePath);
    if (relativePath.endsWith(".cs")) {
      for (const declaration of scanDeclarations(text, relativePath)) {
        symbolLines.push(`${declaration.name}\t${declaration.kind}\t${declaration.path}\t${declaration.startLine}\t${declaration.endLine}`);
        symbolCount++;
      }
    }
  }

  const manifest = lines.join("\n");
  const symbols = symbolLines.join("\n");
  const byPath = new Map(files.map(file => [file.path, file]));
  return { baseDir, roots, files, byPath, manifest, symbols, symbolCount };
}
