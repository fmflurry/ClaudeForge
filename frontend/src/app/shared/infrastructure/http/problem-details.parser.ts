import { HttpErrorResponse } from '@angular/common/http';
import { ApiError, ProblemDetails } from './api-client.types';

/**
 * Parses an HttpErrorResponse into a typed ApiError array.
 * Handles RFC 7807 `application/problem+json` responses.
 */
export function parseProblemDetails(error: HttpErrorResponse): ApiError[] {
  if (error.error && typeof error.error === 'object') {
    const body = error.error as ProblemDetails;

    // RFC 7807 `errors` extension (validation errors)
    if (body.errors && typeof body.errors === 'object') {
      return Object.entries(body.errors).flatMap(([field, messages]) =>
        messages.map((message) => ({
          code: field,
          message,
        })),
      );
    }

    // Single problem detail
    if (body.detail) {
      return [
        {
          code: String(error.status),
          message: body.detail,
        },
      ];
    }

    if (body.title) {
      return [
        {
          code: String(error.status),
          message: body.title,
        },
      ];
    }
  }

  // Fallback
  return [
    {
      code: String(error.status),
      message: error.message || 'An unexpected error occurred.',
    },
  ];
}
