/* eslint-disable @typescript-eslint/consistent-type-definitions */

// ── Dashboard Overview ────────────────────────────────────────────────────────

export type MetricOverview = {
  readonly totalAnalyzed: number;
  readonly totalPassed: number;
  readonly totalFailed: number;
  readonly totalInReview: number;
};

export type QueueStatus = {
  readonly queuedCount: number;
  readonly processingCount: number;
};

export type AppealSummary = {
  readonly pendingAppeals: number;
  readonly avgResolutionTimeHours: number;
};

export type RecentAnalysis = {
  readonly id: string;
  readonly pluginId: string;
  readonly pluginVersion: string;
  readonly status: string;
  readonly totalScore: number;
  readonly createdAt: string;
};

export type TopFinding = {
  readonly finding: string;
  readonly count: number;
};

export type ControlCenterMetrics = {
  readonly overview: MetricOverview;
  readonly queue: QueueStatus;
  readonly appeals: AppealSummary;
  readonly recentAnalyses: number;
  readonly topFindings: readonly TopFinding[];
};

// ── Appeal Detail ─────────────────────────────────────────────────────────────

export type Appeal = {
  readonly appealId: string;
  readonly pluginId: string;
  readonly authorId: string;
  readonly reason: string;
  readonly status: 'pending' | 'approved' | 'rejected';
  readonly reviewedBy: string | null;
  readonly reviewedAt: string | null;
  readonly resolution: string | null;
  readonly createdAt: string;
};

export type AppealDetail = Appeal & {
  readonly pluginName: string | null;
  readonly analysisResultId: string | null;
  readonly evidence: string | null;
  readonly analysisResult: {
    readonly id: string;
    readonly totalScore: number;
    readonly status: string;
    readonly staticScores: {
      readonly eslint: number | null;
      readonly semgrep: number | null;
      readonly gitleaks: number | null;
      readonly trivy: number | null;
    };
    readonly dynamicScore: number | null;
    readonly completedAt: string | null;
  } | null;
};

export type AppealFilter = {
  readonly status?: 'pending' | 'approved' | 'rejected';
  readonly page?: number;
};

// ── Analysis Config ───────────────────────────────────────────────────────────

export type AnalysisConfig = {
  readonly staticWeight: number;
  readonly dynamicWeight: number;
  readonly passThreshold: number;
  readonly failThreshold: number;
  readonly maxWorkers: number;
  readonly retryLimit: number;
  readonly analysisTimeoutSeconds: number;
  readonly updatedAt: string;
  readonly updatedBy: string | null;
};

export type ConfigChangeLog = {
  readonly id: string;
  readonly changedBy: string;
  readonly previousConfig: string;
  readonly newConfig: string;
  readonly changeDescription: string;
  readonly createdAt: string;
};

export type ConfigHistoryResponse = {
  readonly items: readonly ConfigChangeLog[];
  readonly totalCount: number;
  readonly page: number;
  readonly pageSize: number;
};

// ── Audit Log ─────────────────────────────────────────────────────────────────

export type AuditLogEntry = {
  readonly timestamp: string;
  readonly eventType: string;
  readonly description: string;
  readonly actorId: string | null;
  readonly details: Record<string, unknown>;
};

export type AuditLogFilter = {
  readonly from?: string;
  readonly to?: string;
  readonly type?: string;
  readonly page?: number;
};

export type AuditLogResponse = {
  readonly items: readonly AuditLogEntry[];
  readonly totalCount: number;
  readonly page: number;
  readonly pageSize: number;
};

// ── Notification ──────────────────────────────────────────────────────────────

export type Notification = {
  readonly id: string;
  readonly type: string;
  readonly title: string;
  readonly message: string;
  readonly isRead: boolean;
  readonly createdAt: string;
};

export type NotificationPreferences = {
  readonly emailAlerts: boolean;
  readonly inAppAlerts: boolean;
};

// ── Organization management ───────────────────────────────────────────────────

export type Organization = {
  readonly id: string;
  readonly name: string;
  readonly slug: string;
  readonly memberCount: number;
  readonly createdAt: string;
};

export type OrgMember = {
  readonly userId: string;
  readonly email: string;
  readonly displayName: string;
  readonly role: string;
  readonly joinedAt: string;
};

// ── Pagination ────────────────────────────────────────────────────────────────

export type PaginatedResponse<T> = {
  readonly items: readonly T[];
  readonly totalCount: number;
  readonly page: number;
  readonly pageSize: number;
};
