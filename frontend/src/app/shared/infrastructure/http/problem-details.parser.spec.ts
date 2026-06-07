/**
 * Unit tests for parseProblemDetails — pure function.
 */

import { HttpErrorResponse } from '@angular/common/http';
import { parseProblemDetails } from './problem-details.parser';

function makeError(status: number, body: unknown, statusText = 'Error'): HttpErrorResponse {
  return new HttpErrorResponse({ error: body, status, statusText, url: '/test' });
}

// ---------------------------------------------------------------------------
// RFC 7807 `errors` extension (validation errors)
// ---------------------------------------------------------------------------

describe('parseProblemDetails — validation errors (errors extension)', () => {
  it('should parse single field validation error', () => {
    const error = makeError(422, {
      errors: { email: ['Email is invalid'] },
    });
    const result = parseProblemDetails(error);
    expect(result).toHaveLength(1);
    expect(result[0].code).toBe('email');
    expect(result[0].message).toBe('Email is invalid');
  });

  it('should parse multiple messages for a single field', () => {
    const error = makeError(422, {
      errors: { password: ['Too short', 'Missing uppercase'] },
    });
    const result = parseProblemDetails(error);
    expect(result).toHaveLength(2);
    expect(result[0].message).toBe('Too short');
    expect(result[1].message).toBe('Missing uppercase');
  });

  it('should parse multiple fields', () => {
    const error = makeError(422, {
      errors: {
        name: ['Name is required'],
        slug: ['Slug is taken'],
      },
    });
    const result = parseProblemDetails(error);
    expect(result).toHaveLength(2);
    const codes = result.map((e) => e.code);
    expect(codes).toContain('name');
    expect(codes).toContain('slug');
  });

  it('should set code to field name and message to the error text', () => {
    const error = makeError(422, {
      errors: { username: ['Already taken'] },
    });
    const result = parseProblemDetails(error);
    expect(result[0].code).toBe('username');
    expect(result[0].message).toBe('Already taken');
  });
});

// ---------------------------------------------------------------------------
// Single problem detail (detail field)
// ---------------------------------------------------------------------------

describe('parseProblemDetails — single problem (detail field)', () => {
  it('should parse detail field', () => {
    const error = makeError(404, {
      type: 'https://tools.ietf.org/html/rfc7231#section-6.5.4',
      title: 'Not Found',
      status: 404,
      detail: 'Plugin not found',
    });
    const result = parseProblemDetails(error);
    expect(result).toHaveLength(1);
    expect(result[0].message).toBe('Plugin not found');
    expect(result[0].code).toBe('404');
  });

  it('should use HTTP status as code for detail errors', () => {
    const error = makeError(403, { detail: 'Forbidden' });
    const result = parseProblemDetails(error);
    expect(result[0].code).toBe('403');
  });
});

// ---------------------------------------------------------------------------
// Title field fallback
// ---------------------------------------------------------------------------

describe('parseProblemDetails — title field fallback', () => {
  it('should parse title when detail is absent', () => {
    const error = makeError(500, {
      title: 'Internal Server Error',
      status: 500,
    });
    const result = parseProblemDetails(error);
    expect(result).toHaveLength(1);
    expect(result[0].message).toBe('Internal Server Error');
    expect(result[0].code).toBe('500');
  });

  it('should prefer detail over title when both are present', () => {
    const error = makeError(400, {
      title: 'Bad Request',
      detail: 'Specific detail message',
    });
    const result = parseProblemDetails(error);
    expect(result[0].message).toBe('Specific detail message');
  });
});

// ---------------------------------------------------------------------------
// Non-object body fallback
// ---------------------------------------------------------------------------

describe('parseProblemDetails — non-object body (fallback)', () => {
  it('should return fallback error when body is null', () => {
    const error = makeError(500, null);
    const result = parseProblemDetails(error);
    expect(result).toHaveLength(1);
    expect(result[0].code).toBe('500');
  });

  it('should return fallback error when body is a string', () => {
    const error = makeError(500, 'Internal Server Error');
    const result = parseProblemDetails(error);
    expect(result).toHaveLength(1);
  });

  it('should use error.message in fallback', () => {
    const err = new HttpErrorResponse({
      error: null,
      status: 503,
      statusText: 'Service Unavailable',
      url: '/test',
    });
    const result = parseProblemDetails(err);
    expect(result[0].code).toBe('503');
    expect(typeof result[0].message).toBe('string');
    expect(result[0].message.length).toBeGreaterThan(0);
  });

  it('should return fallback when body is an empty object with no detail/title/errors', () => {
    const error = makeError(400, {});
    const result = parseProblemDetails(error);
    expect(result).toHaveLength(1);
    expect(result[0].code).toBe('400');
  });
});

// ---------------------------------------------------------------------------
// Return shape
// ---------------------------------------------------------------------------

describe('parseProblemDetails — return shape', () => {
  it('should always return an array', () => {
    const error = makeError(500, null);
    const result = parseProblemDetails(error);
    expect(Array.isArray(result)).toBe(true);
  });

  it('each entry should have code (string) and message (string)', () => {
    const error = makeError(422, { errors: { email: ['Invalid'] } });
    const result = parseProblemDetails(error);
    for (const entry of result) {
      expect(typeof entry.code).toBe('string');
      expect(typeof entry.message).toBe('string');
    }
  });
});
