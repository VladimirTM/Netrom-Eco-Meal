export interface ReportViewDto {
  id: string;
  targetType: string;
  targetId: string;
  targetName: string;
  reason: string;
  status: string;
  reporterName: string;
  createdAt: string;
  resolvedAt: string | null;
}
