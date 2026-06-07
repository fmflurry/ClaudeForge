/**
 * Domain validation rules for team identifiers.
 * Pure functions — zero framework or infrastructure dependencies.
 */

export interface TeamIdValidationResult {
  valid: boolean;
  error?: string;
  normalized?: string;
}

export const MIN_TEAM_ID_LENGTH = 2;
export const MAX_TEAM_ID_LENGTH = 50;

/**
 * Ordered preset team names that are guaranteed to pass validateTeamId.
 */
export const PRESET_TEAMS: readonly string[] = ['Engineering', 'Product', 'Design', 'QA', 'DevOps'] as const;

/**
 * Allowed characters: letters (a-z, A-Z), digits (0-9), hyphens (-),
 * underscores (_), and spaces ( ).
 */
const ALLOWED_CHARS_PATTERN = /^[a-zA-Z0-9_\- ]+$/;

/**
 * Validates a raw team identifier string.
 *
 * Steps:
 *   1. Trim leading/trailing whitespace.
 *   2. Reject empty/whitespace-only strings.
 *   3. Reject length below MIN_TEAM_ID_LENGTH.
 *   4. Reject length above MAX_TEAM_ID_LENGTH.
 *   5. Reject strings containing characters outside [a-zA-Z0-9_\- ].
 *   6. On success, return { valid: true, normalized: trimmedId }.
 *   7. On failure, return { valid: false, error: string }.
 */
export function validateTeamId(raw: string): TeamIdValidationResult {
  const trimmed = raw.trim();

  if (trimmed.length === 0) {
    return { valid: false, error: 'Team ID must not be empty.' };
  }

  if (trimmed.length < MIN_TEAM_ID_LENGTH) {
    return {
      valid: false,
      error: `Team ID must be at least ${MIN_TEAM_ID_LENGTH} characters long.`,
    };
  }

  if (trimmed.length > MAX_TEAM_ID_LENGTH) {
    return {
      valid: false,
      error: `Team ID must be at most ${MAX_TEAM_ID_LENGTH} characters long.`,
    };
  }

  if (!ALLOWED_CHARS_PATTERN.test(trimmed)) {
    return {
      valid: false,
      error: 'Team ID may only contain letters, digits, hyphens, underscores, and spaces.',
    };
  }

  return { valid: true, normalized: trimmed };
}
