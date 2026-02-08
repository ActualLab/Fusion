/** Common interface for types that carry a value-or-error outcome. */
export interface IResult<T> {
  readonly hasValue: boolean;
  readonly hasError: boolean;
  readonly value: T;
  readonly error: unknown;
  readonly valueOrUndefined: T | undefined;
}

/** Immutable value-or-error container — similar to .NET's Result<T>. */
export class Result<T> implements IResult<T> {
  readonly hasValue: boolean;
  readonly hasError: boolean;
  private readonly _value: T | undefined;
  private readonly _error: unknown;

  public constructor(value: T | undefined, error?: unknown) {
    this.hasValue = error === undefined;
    this.hasError = !this.hasValue;
    this._value = value;
    this._error = error;
  }

  get value(): T {
    if (this.hasError) throw this._error;
    return this._value as T;
  }

  get error(): unknown {
    return this._error;
  }

  get valueOrUndefined(): T | undefined {
    return this.hasValue ? this._value : undefined;
  }

  static ok<T>(value: T): Result<T> {
    return new Result<T>(value);
  }

  static error<T>(error: unknown): Result<T> {
    return new Result<T>(undefined, error);
  }
}

export function result<T>(value: T, error?: unknown): Result<T> {
  return new Result<T>(value, error);
}

export function errorResult<T>(error: unknown): Result<T> {
  return Result.error(error);
}

export function resultFrom<T>(fn: () => T): Result<T> {
  try {
    return result(fn());
  } catch (e) {
    return errorResult(e);
  }
}

export async function resultFromAsync<T>(fn: () => Promise<T>): Promise<Result<T>> {
  try {
    return result(await fn());
  } catch (e) {
    return errorResult(e);
  }
}
