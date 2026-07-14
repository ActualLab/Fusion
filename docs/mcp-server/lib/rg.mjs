import { spawn } from "node:child_process";

export const RG_DEFAULTS = {
  bin: process.env.RG_BIN ?? "rg",
  outputLimitBytes: Number(process.env.RG_OUTPUT_LIMIT ?? 64 * 1024),
  timeoutMs: Number(process.env.RG_TIMEOUT_MS ?? 1000),
  threads: Number(process.env.RG_THREADS ?? 2),
  maxConcurrency: Number(process.env.RG_MAX_CONCURRENCY ?? 3),
  queueWaitMs: Number(process.env.RG_QUEUE_WAIT_MS ?? 2000),
  nice: (process.env.RG_NICE ?? (process.platform === "linux" ? "1" : "0")) === "1",
};

let active = 0;
const waiters = [];

function acquire(waitMs) {
  if (active < RG_DEFAULTS.maxConcurrency) {
    active++;
    return Promise.resolve(true);
  }
  return new Promise(resolve => {
    const timer = setTimeout(() => {
      const index = waiters.indexOf(entry);
      if (index >= 0)
        waiters.splice(index, 1);
      resolve(false);
    }, waitMs);
    const entry = () => {
      clearTimeout(timer);
      active++;
      resolve(true);
    };
    waiters.push(entry);
  });
}

function release() {
  active--;
  const next = waiters.shift();
  if (next)
    next();
}

function buildCommand(args) {
  if (RG_DEFAULTS.nice)
    return { file: "nice", argv: ["-n", "15", "ionice", "-c", "3", RG_DEFAULTS.bin, ...args] };
  return { file: RG_DEFAULTS.bin, argv: args };
}

export async function runRipgrep(args, options = {}) {
  const outputLimit = options.outputLimitBytes ?? RG_DEFAULTS.outputLimitBytes;
  const timeoutMs = options.timeoutMs ?? RG_DEFAULTS.timeoutMs;
  const cwd = options.cwd;

  const acquired = await acquire(RG_DEFAULTS.queueWaitMs);
  if (!acquired)
    return { ok: false, busy: true, stdout: "", bytes: 0, truncated: false, timedOut: false, elapsedMs: 0 };

  const fullArgs = ["--threads", String(RG_DEFAULTS.threads), ...args];
  const { file, argv } = buildCommand(fullArgs);
  const startedAt = process.hrtime.bigint();

  return await new Promise(resolve => {
    const chunks = [];
    let bytes = 0;
    let truncated = false;
    let timedOut = false;
    let settled = false;

    const child = spawn(file, argv, { cwd, stdio: ["ignore", "pipe", "ignore"] });

    const finish = () => {
      if (settled)
        return;
      settled = true;
      clearTimeout(watchdog);
      release();
      const elapsedMs = Number(process.hrtime.bigint() - startedAt) / 1e6;
      resolve({ ok: true, busy: false, stdout: Buffer.concat(chunks).toString("utf8"), bytes, truncated, timedOut, elapsedMs });
    };

    const watchdog = setTimeout(() => {
      timedOut = true;
      child.kill("SIGKILL");
    }, timeoutMs);

    child.stdout.on("data", chunk => {
      if (truncated)
        return;
      const remaining = outputLimit - bytes;
      if (chunk.length >= remaining) {
        chunks.push(chunk.subarray(0, Math.max(0, remaining)));
        bytes = outputLimit;
        truncated = true;
        child.kill("SIGKILL");
        return;
      }
      chunks.push(chunk);
      bytes += chunk.length;
    });

    child.on("error", finish);
    child.on("close", finish);
  });
}
