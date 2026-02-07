/** Controls when state re-computation happens after invalidation. */
export interface UpdateDelayer {
  delay(): Promise<void>;
}

/** Re-computes immediately with no delay. */
export class NoDelayer implements UpdateDelayer {
  async delay(): Promise<void> {
    // No delay — return immediately
  }
}

/** Re-computes after a fixed delay in milliseconds. */
export class FixedDelayer implements UpdateDelayer {
  readonly ms: number;

  constructor(ms: number) {
    this.ms = ms;
  }

  async delay(): Promise<void> {
    await new Promise<void>((r) => setTimeout(r, this.ms));
  }
}
