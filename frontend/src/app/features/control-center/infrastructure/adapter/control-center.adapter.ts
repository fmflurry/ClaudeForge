import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { map, Observable } from 'rxjs';
import { API_BASE_URL } from '../../../../core/config/api-config';
import { ControlCenterPort } from '../../domain/ports/control-center.port';
import type {
  AnalysisConfig,
  Appeal,
  AppealDetail,
  AppealFilter,
  AuditLogFilter,
  AuditLogResponse,
  ConfigHistoryResponse,
  ControlCenterMetrics,
  Notification,
  NotificationPreferences,
  Organization,
  OrgMember,
} from '../../domain/models/control-center.models';

@Injectable()
export class ControlCenterAdapter extends ControlCenterPort {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);
  private readonly api = `${this.baseUrl}/api/v1`;

  // ── Metrics ──────────────────────────────────────────────────────────────

  getMetrics(): Observable<ControlCenterMetrics> {
    return this.http.get<ControlCenterMetrics>(`${this.api}/control-center/metrics`);
  }

  // ── Appeals ──────────────────────────────────────────────────────────────

  getAppeals(filter: AppealFilter): Observable<{ items: Appeal[]; totalCount: number; page: number; pageSize: number }> {
    let params = new HttpParams();
    if (filter.status) params = params.set('status', filter.status);
    if (filter.page) params = params.set('page', filter.page);
    return this.http.get<{ items: Appeal[]; totalCount: number; page: number; pageSize: number }>(
      `${this.api}/control-center/appeals`,
      { params },
    );
  }

  getAppealDetail(appealId: string): Observable<AppealDetail> {
    return this.http.get<AppealDetail>(`${this.api}/control-center/appeals/${appealId}`);
  }

  resolveAppeal(appealId: string, resolution: string, notes?: string): Observable<unknown> {
    return this.http.put(`${this.api}/control-center/appeals/${appealId}`, { resolution, notes });
  }

  // ── Config ───────────────────────────────────────────────────────────────

  getAnalysisConfig(): Observable<AnalysisConfig> {
    return this.http.get<AnalysisConfig>(`${this.api}/control-center/config/analysis`);
  }

  updateAnalysisConfig(config: Partial<AnalysisConfig>): Observable<unknown> {
    return this.http.put(`${this.api}/control-center/config/analysis`, config);
  }

  getConfigHistory(page?: number): Observable<ConfigHistoryResponse> {
    let params = new HttpParams();
    if (page) params = params.set('page', page);
    return this.http.get<ConfigHistoryResponse>(`${this.api}/control-center/config/history`, { params });
  }

  // ── Audit Logs ──────────────────────────────────────────────────────────

  getAuditLogs(filter: AuditLogFilter): Observable<AuditLogResponse> {
    let params = new HttpParams();
    if (filter.from) params = params.set('from', filter.from);
    if (filter.to) params = params.set('to', filter.to);
    if (filter.type) params = params.set('type', filter.type);
    if (filter.page) params = params.set('page', filter.page);
    return this.http.get<AuditLogResponse>(`${this.api}/control-center/audit-logs`, { params });
  }

  // ── Notifications ───────────────────────────────────────────────────────

  getNotifications(unreadOnly?: boolean, page?: number): Observable<{ items: Notification[]; totalCount: number; page: number; pageSize: number }> {
    let params = new HttpParams();
    if (unreadOnly) params = params.set('unreadOnly', 'true');
    if (page) params = params.set('page', page);
    return this.http.get<{ items: Notification[]; totalCount: number; page: number; pageSize: number }>(
      `${this.api}/notifications`,
      { params },
    );
  }

  markNotificationRead(notificationId: string): Observable<unknown> {
    return this.http.put(`${this.api}/notifications/${notificationId}/read`, {});
  }

  markAllNotificationsRead(): Observable<unknown> {
    return this.http.put(`${this.api}/notifications/read-all`, {});
  }

  updateNotificationPreferences(prefs: NotificationPreferences): Observable<unknown> {
    return this.http.put(`${this.api}/notifications/preferences`, prefs);
  }

  // ── Organizations ───────────────────────────────────────────────────────

  getOrganizations(): Observable<Organization[]> {
    return this.http.get<Organization[]>(`${this.api}/organizations`);
  }

  getOrgDetail(orgId: string): Observable<Organization> {
    return this.http.get<Organization>(`${this.api}/organizations/${orgId}`);
  }

  getOrgMembers(orgId: string): Observable<OrgMember[]> {
    return this.http.get<OrgMember[]>(`${this.api}/organizations/${orgId}/members`);
  }

  inviteMember(orgId: string, email: string, role: string): Observable<unknown> {
    return this.http.post(`${this.api}/organizations/${orgId}/invitations`, { email, role });
  }

  removeMember(orgId: string, userId: string): Observable<unknown> {
    return this.http.delete(`${this.api}/organizations/${orgId}/members/${userId}`);
  }
}
