// .NET counterpart: ActualLab.Time.RetryDelaySeq

const DefaultSpread = 0.1;
const DefaultMultiplier = 1.41421356237; // Math.sqrt(2)

function applySpread(origin: number, spread: number): number {
  if (spread <= 0) return origin;
  const delta = origin * spread;
  return origin + (Math.random() * 2 - 1) * delta;
}

export class RetryDelaySeq {
  readonly min: number;
  readonly max: number;
  readonly spread: number;
  readonly multiplier: number;

  constructor(min: number, max: number, spread: number = DefaultSpread, multiplier: number = DefaultMultiplier) {
    this.min = min;
    this.max = max;
    this.spread = spread;
    this.multiplier = multiplier;
  }

  static fixed(delayMs: number, spread: number = DefaultSpread): RetryDelaySeq {
    return new RetryDelaySeq(delayMs, delayMs, spread, 1);
  }

  static exp(
    minMs: number,
    maxMs: number,
    spread: number = DefaultSpread,
    multiplier: number = DefaultMultiplier,
  ): RetryDelaySeq {
    return new RetryDelaySeq(minMs, maxMs, spread, multiplier);
  }

  getDelay(failureCount: number): number {
    if (this.min < 0) throw new Error("RetryDelaySeq.min must be non-negative.");
    if (failureCount <= 0) return 0;

    if (this.multiplier <= 1) {
      // Fixed — no exponential component
      return Math.max(0, this.spread <= 0 ? this.min : applySpread(this.min, this.spread));
    }

    const multiplier = Math.pow(this.multiplier, failureCount - 1);
    const raw = Math.min(this.max, this.min * multiplier);
    return Math.max(0, applySpread(raw, this.spread));
  }
}
