import { Observable } from 'rxjs';
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
} from '../models/control-center.models';

export abstract class ControlCenterPort {
  // Metrics
  abstract getMetrics(): Observable<ControlCenterMetrics>;

  // Appeals
  abstract getAppeals(
    filter: AppealFilter,
  ): Observable<{ items: Appeal[]; totalCount: number; page: number; pageSize: number }>;
  abstract getAppealDetail(appealId: string): Observable<AppealDetail>;
  abstract resolveAppeal(appealId: string, resolution: string, notes?: string): Observable<unknown>;

  // Config
  abstract getAnalysisConfig(): Observable<AnalysisConfig>;
  abstract updateAnalysisConfig(config: Partial<AnalysisConfig>): Observable<unknown>;
  abstract getConfigHistory(page?: number): Observable<ConfigHistoryResponse>;

  // Audit logs
  abstract getAuditLogs(filter: AuditLogFilter): Observable<AuditLogResponse>;

  // Notifications
  abstract getNotifications(
    unreadOnly?: boolean,
    page?: number,
  ): Observable<{ items: Notification[]; totalCount: number; page: number; pageSize: number }>;
  abstract markNotificationRead(notificationId: string): Observable<unknown>;
  abstract markAllNotificationsRead(): Observable<unknown>;
  abstract updateNotificationPreferences(prefs: NotificationPreferences): Observable<unknown>;

  // Organizations
  abstract getOrganizations(): Observable<Organization[]>;
  abstract getOrgDetail(orgId: string): Observable<Organization>;
  abstract getOrgMembers(orgId: string): Observable<OrgMember[]>;
  abstract inviteMember(orgId: string, email: string, role: string): Observable<unknown>;
  abstract removeMember(orgId: string, userId: string): Observable<unknown>;
}
