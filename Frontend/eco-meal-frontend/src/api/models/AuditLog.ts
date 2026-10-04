export interface AuditLogDto {
  id: string;
  actorUserId: string;
  actorName: string;
  action: string;
  targetType: string;
  targetId: string | null;
  targetName: string;
  details: string | null;
  createdAt: string;
}
