/**
 * HTTP adapter implementing DeviceActivationPort.
 *
 * Endpoint:
 *   POST /auth/device/approve   body: { userCode: string }
 *
 * The auth interceptor attaches the Bearer token automatically because
 * /auth/device/approve is NOT on the interceptor skip-list.
 * withCredentials: true is also set so the HttpOnly cookie is sent.
 *
 * Error responses are caught and mapped to DeviceApprovalResult discriminants;
 * no unhandled Observable errors are propagated.
 */

import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { catchError, map, Observable, of } from 'rxjs';
import { API_BASE_URL } from '../../../../core/config/api-config';
import { DeviceActivationPort } from '../../domain/ports/device-activation.port';
import type { DeviceApprovalResult } from '../../domain/ports/device-activation.port';

interface ApproveRequestBody {
  userCode: string;
}

function mapHttpError(err: unknown): DeviceApprovalResult {
  if (err instanceof HttpErrorResponse) {
    switch (err.status) {
      case 400:
        return { kind: 'Invalid' };
      case 404:
        return { kind: 'NotFound' };
      case 409:
        return { kind: 'AlreadyApproved' };
      case 410:
        return { kind: 'Expired' };
      case 401:
      case 403:
        return { kind: 'Unauthorized' };
      default:
        return { kind: 'Unauthorized' };
    }
  }
  return { kind: 'Unauthorized' };
}

@Injectable()
export class DeviceActivationHttpAdapter extends DeviceActivationPort {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  approve(userCode: string): Observable<DeviceApprovalResult> {
    const body: ApproveRequestBody = { userCode };
    return this.http
      .post<unknown>(`${this.baseUrl}/auth/device/approve`, body, {
        withCredentials: true,
      })
      .pipe(
        map((): DeviceApprovalResult => ({ kind: 'Approved' })),
        catchError((err: unknown): Observable<DeviceApprovalResult> => of(mapHttpError(err))),
      );
  }
}
