let _version = 0;

/** Returns a globally unique monotonically increasing version number. */
export function nextVersion(): number {
  return ++_version;
}
