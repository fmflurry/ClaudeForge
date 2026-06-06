/**
 * Domain port for anonymous identifier management.
 * Supports generation, rotation, and deletion of the anonymised client ID.
 */
export abstract class AnonIdPort {
  /**
   * Return the persisted anon-id, generating a fresh one if absent.
   */
  abstract getOrCreate(): Promise<string>;

  /**
   * Clear stored id and generate a fresh one (rotation).
   */
  abstract rotate(): Promise<string>;

  /**
   * Clear stored id from storage (e.g. on full opt-out clear).
   */
  abstract clear(): void;
}
