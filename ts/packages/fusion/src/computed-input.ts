/** Identity key for a computed value — combines a service/method identity with serialized arguments. */
export class ComputedInput {
  readonly key: string;

  constructor(
    readonly serviceId: string,
    readonly methodName: string,
    readonly args: unknown[],
  ) {
    // Key format: "ServiceId.methodName:arg1,arg2,..."
    this.key = `${serviceId}.${methodName}:${args.map(a => JSON.stringify(a)).join(",")}`;
  }

  equals(other: ComputedInput): boolean {
    return this.key === other.key;
  }

  toString(): string {
    return this.key;
  }
}
