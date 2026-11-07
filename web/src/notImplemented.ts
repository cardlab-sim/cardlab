/**
 * Skeleton stub marker (002 PR-00): throws so an unimplemented path fails loudly.
 * Arguments are accepted (and ignored) so stubs keep their real parameter names without lint noise.
 * @param pr - The PR that implements the stub, e.g. "PR-03".
 * @param _args - The stub's own parameters.
 * @throws Error always.
 */
export function notImplemented(pr: string, ..._args: unknown[]): never {
  throw new Error(`NotImplemented: ${pr}`);
}
