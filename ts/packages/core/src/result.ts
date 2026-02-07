/** Discriminated union representing either a value or an error — similar to .NET's Result<T>. */
export type Result<T> = ResultOk<T> | ResultError;

export interface ResultOk<T> {
  readonly ok: true;
  readonly value: T;
}

export interface ResultError {
  readonly ok: false;
  readonly error: unknown;
}

export function ok<T>(value: T): ResultOk<T> {
  return { ok: true, value };
}

export function error(error: unknown): ResultError {
  return { ok: false, error };
}

export function resultFrom<T>(fn: () => T): Result<T> {
  try {
    return ok(fn());
  } catch (e) {
    return error(e);
  }
}

export async function resultFromAsync<T>(fn: () => Promise<T>): Promise<Result<T>> {
  try {
    return ok(await fn());
  } catch (e) {
    return error(e);
  }
}

export function resultValue<T>(result: Result<T>): T {
  if (result.ok) return result.value;
  throw result.error;
}
