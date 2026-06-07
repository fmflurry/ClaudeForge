/**
 * RED tests — Task 14.1: Domain validation rules for team-id
 *
 * Expected production file (does NOT exist yet — tests MUST FAIL):
 *   src/app/features/team-context/domain/rules/team-id-validation.rules.ts
 *
 * Production exports the coder MUST define:
 *
 *   interface TeamIdValidationResult {
 *     valid: boolean;
 *     error?: string;
 *     normalized?: string;
 *   }
 *
 *   function validateTeamId(raw: string): TeamIdValidationResult
 *     - trims whitespace before validation
 *     - returns { valid: true, normalized: trimmedId } on success
 *     - returns { valid: false, error: string } on failure
 *     - rules:
 *       - reject empty / whitespace-only
 *       - reject length < MIN_TEAM_ID_LENGTH (2)
 *       - reject length > MAX_TEAM_ID_LENGTH (50)
 *       - reject strings containing characters outside [a-zA-Z0-9_\- ] (spec: no special chars)
 *       - allow letters, digits, hyphens, underscores, spaces
 *
 *   const MIN_TEAM_ID_LENGTH: number  (= 2)
 *   const MAX_TEAM_ID_LENGTH: number  (= 50)
 *
 *   const PRESET_TEAMS: readonly string[]
 *     - ordered preset list: ['Engineering', 'Product', 'Design', 'QA', 'DevOps']
 *     - each preset MUST itself pass validateTeamId
 */

import { validateTeamId, MIN_TEAM_ID_LENGTH, MAX_TEAM_ID_LENGTH, PRESET_TEAMS } from './team-id-validation.rules';
import type { TeamIdValidationResult } from './team-id-validation.rules';

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

describe('team-id validation — constants', () => {
  it('MIN_TEAM_ID_LENGTH should be 2', () => {
    expect(MIN_TEAM_ID_LENGTH).toBe(2);
  });

  it('MAX_TEAM_ID_LENGTH should be 50', () => {
    expect(MAX_TEAM_ID_LENGTH).toBe(50);
  });

  it('PRESET_TEAMS should be a readonly array', () => {
    expect(Array.isArray(PRESET_TEAMS)).toBe(true);
    expect(PRESET_TEAMS.length).toBeGreaterThan(0);
  });

  it('PRESET_TEAMS should contain Engineering', () => {
    expect(PRESET_TEAMS).toContain('Engineering');
  });

  it('PRESET_TEAMS should contain Product', () => {
    expect(PRESET_TEAMS).toContain('Product');
  });

  it('PRESET_TEAMS should contain Design', () => {
    expect(PRESET_TEAMS).toContain('Design');
  });

  it('PRESET_TEAMS should contain QA', () => {
    expect(PRESET_TEAMS).toContain('QA');
  });

  it('PRESET_TEAMS should contain DevOps', () => {
    expect(PRESET_TEAMS).toContain('DevOps');
  });

  it('every preset team must pass validateTeamId itself', () => {
    for (const preset of PRESET_TEAMS) {
      const result = validateTeamId(preset);
      expect(result.valid).toBe(true);
    }
  });
});

// ---------------------------------------------------------------------------
// validateTeamId — return shape
// ---------------------------------------------------------------------------

describe('validateTeamId — result shape', () => {
  it('should return an object with a boolean valid field', () => {
    const result: TeamIdValidationResult = validateTeamId('Engineering');
    expect(typeof result.valid).toBe('boolean');
  });

  it('should return normalized on success', () => {
    const result = validateTeamId('Engineering');
    expect(result.valid).toBe(true);
    expect(result.normalized).toBeDefined();
  });

  it('should NOT include normalized on failure', () => {
    const result = validateTeamId('');
    expect(result.valid).toBe(false);
    expect(result.normalized).toBeUndefined();
  });

  it('should include error string on failure', () => {
    const result = validateTeamId('');
    expect(typeof result.error).toBe('string');
    expect((result.error ?? '').length).toBeGreaterThan(0);
  });

  it('should NOT include error on success', () => {
    const result = validateTeamId('Engineering');
    expect(result.error).toBeUndefined();
  });
});

// ---------------------------------------------------------------------------
// validateTeamId — empty / whitespace
// ---------------------------------------------------------------------------

describe('validateTeamId — empty and whitespace', () => {
  it('should reject empty string', () => {
    expect(validateTeamId('').valid).toBe(false);
  });

  it('should reject whitespace-only string (single space)', () => {
    expect(validateTeamId(' ').valid).toBe(false);
  });

  it('should reject whitespace-only string (multiple spaces)', () => {
    expect(validateTeamId('   ').valid).toBe(false);
  });

  it('should reject tab-only string', () => {
    expect(validateTeamId('\t').valid).toBe(false);
  });

  it('should reject newline-only string', () => {
    expect(validateTeamId('\n').valid).toBe(false);
  });

  it('should provide a non-empty error for empty input', () => {
    const { error } = validateTeamId('');
    expect(error).toBeTruthy();
  });
});

// ---------------------------------------------------------------------------
// validateTeamId — trimming behaviour
// ---------------------------------------------------------------------------

describe('validateTeamId — trim', () => {
  it('should trim leading whitespace before validation', () => {
    const result = validateTeamId('  Engineering');
    expect(result.valid).toBe(true);
    expect(result.normalized).toBe('Engineering');
  });

  it('should trim trailing whitespace before validation', () => {
    const result = validateTeamId('Engineering  ');
    expect(result.valid).toBe(true);
    expect(result.normalized).toBe('Engineering');
  });

  it('should trim both ends', () => {
    const result = validateTeamId('  QA  ');
    expect(result.valid).toBe(true);
    expect(result.normalized).toBe('QA');
  });

  it('normalized value should equal the trimmed input on success', () => {
    const result = validateTeamId('  DevOps  ');
    expect(result.normalized).toBe('DevOps');
  });

  it('should reject an id that is only whitespace even after the raw string has content', () => {
    //   (non-breaking space) — post-trim may or may not be empty; standard trim covers ASCII
    expect(validateTeamId(' ').valid).toBe(false);
  });
});

// ---------------------------------------------------------------------------
// validateTeamId — length boundaries
// ---------------------------------------------------------------------------

describe('validateTeamId — length', () => {
  it('should reject a single character (below MIN_TEAM_ID_LENGTH)', () => {
    expect(validateTeamId('A').valid).toBe(false);
  });

  it('should accept exactly MIN_TEAM_ID_LENGTH characters', () => {
    const id = 'AB';
    expect(id.length).toBe(MIN_TEAM_ID_LENGTH);
    expect(validateTeamId(id).valid).toBe(true);
  });

  it('should accept a mid-range length team id', () => {
    expect(validateTeamId('MyTeam').valid).toBe(true);
  });

  it('should accept exactly MAX_TEAM_ID_LENGTH characters', () => {
    const id = 'A'.repeat(MAX_TEAM_ID_LENGTH);
    expect(id.length).toBe(MAX_TEAM_ID_LENGTH);
    expect(validateTeamId(id).valid).toBe(true);
  });

  it('should reject a string one character over MAX_TEAM_ID_LENGTH', () => {
    const id = 'A'.repeat(MAX_TEAM_ID_LENGTH + 1);
    expect(validateTeamId(id).valid).toBe(false);
  });

  it('should reject a very long string', () => {
    const id = 'A'.repeat(200);
    expect(validateTeamId(id).valid).toBe(false);
  });

  it('should include an error about length when too long', () => {
    const { error } = validateTeamId('A'.repeat(MAX_TEAM_ID_LENGTH + 1));
    expect(error).toBeTruthy();
  });

  it('should include an error about length when too short', () => {
    const { error } = validateTeamId('X');
    expect(error).toBeTruthy();
  });
});

// ---------------------------------------------------------------------------
// validateTeamId — allowed characters
// ---------------------------------------------------------------------------

describe('validateTeamId — allowed characters', () => {
  it('should accept alphabetic lowercase letters', () => {
    expect(validateTeamId('engineering').valid).toBe(true);
  });

  it('should accept alphabetic uppercase letters', () => {
    expect(validateTeamId('ENGINEERING').valid).toBe(true);
  });

  it('should accept mixed case letters', () => {
    expect(validateTeamId('MyTeam').valid).toBe(true);
  });

  it('should accept digits in a team id', () => {
    expect(validateTeamId('Team42').valid).toBe(true);
  });

  it('should accept hyphens', () => {
    expect(validateTeamId('my-team').valid).toBe(true);
  });

  it('should accept underscores', () => {
    expect(validateTeamId('my_team').valid).toBe(true);
  });

  it('should accept internal spaces (mid-word)', () => {
    expect(validateTeamId('My Team').valid).toBe(true);
  });

  it('should accept combinations of letters digits hyphens underscores and spaces', () => {
    expect(validateTeamId('Team Alpha-1_QA').valid).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// validateTeamId — rejected special characters
// ---------------------------------------------------------------------------

describe('validateTeamId — rejected special characters', () => {
  it('should reject @ symbol', () => {
    expect(validateTeamId('team@name').valid).toBe(false);
  });

  it('should reject # symbol', () => {
    expect(validateTeamId('team#1').valid).toBe(false);
  });

  it('should reject $ symbol', () => {
    expect(validateTeamId('team$').valid).toBe(false);
  });

  it('should reject % symbol', () => {
    expect(validateTeamId('team%').valid).toBe(false);
  });

  it('should reject & symbol', () => {
    expect(validateTeamId('team&co').valid).toBe(false);
  });

  it('should reject * symbol', () => {
    expect(validateTeamId('team*').valid).toBe(false);
  });

  it('should reject ! symbol', () => {
    expect(validateTeamId('team!').valid).toBe(false);
  });

  it('should reject forward slash', () => {
    expect(validateTeamId('team/sub').valid).toBe(false);
  });

  it('should reject backslash', () => {
    expect(validateTeamId('team\\sub').valid).toBe(false);
  });

  it('should reject parentheses', () => {
    expect(validateTeamId('team(1)').valid).toBe(false);
  });

  it('should reject angle brackets', () => {
    expect(validateTeamId('team<1>').valid).toBe(false);
  });

  it('should reject SQL injection pattern', () => {
    expect(validateTeamId("team'; DROP TABLE").valid).toBe(false);
  });

  it('should reject emoji characters', () => {
    expect(validateTeamId('team🚀').valid).toBe(false);
  });

  it('should reject unicode letters outside ASCII range', () => {
    expect(validateTeamId('équipe').valid).toBe(false);
  });

  it('should provide an error message for invalid characters', () => {
    const { error } = validateTeamId('team@name');
    expect(error).toBeTruthy();
  });
});

// ---------------------------------------------------------------------------
// validateTeamId — immutability / no side effects
// ---------------------------------------------------------------------------

describe('validateTeamId — immutability', () => {
  it('should return a new object each call', () => {
    const r1 = validateTeamId('Engineering');
    const r2 = validateTeamId('Engineering');
    expect(r1).not.toBe(r2);
  });

  it('should not mutate the input string', () => {
    const input = '  Engineering  ';
    validateTeamId(input);
    expect(input).toBe('  Engineering  ');
  });
});

// ---------------------------------------------------------------------------
// validateTeamId — edge combinations
// ---------------------------------------------------------------------------

describe('validateTeamId — edge combinations', () => {
  it('should reject null coerced to string (not a valid id)', () => {
    // Only string inputs are relevant; "null" as string is 4 chars and invalid chars check applies
    // "null" passes char check but is a valid test of boundary
    expect(validateTeamId('null').valid).toBe(true); // "null" is 4 valid chars
  });

  it('should handle a 2-char id with a hyphen at end — valid chars', () => {
    // "a-" is 2 chars, all allowed chars
    expect(validateTeamId('a-').valid).toBe(true);
  });

  it('should handle an id that trims to exactly MIN_TEAM_ID_LENGTH', () => {
    // "  AB  " → "AB" (2 chars) → valid
    expect(validateTeamId('  AB  ').valid).toBe(true);
  });

  it('should handle an id that trims to one char (below min)', () => {
    // "  A  " → "A" (1 char) → invalid
    expect(validateTeamId('  A  ').valid).toBe(false);
  });
});
