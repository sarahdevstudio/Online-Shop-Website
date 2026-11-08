/** Shapes of `/api/v1/admin/reviews` (docs/18). `rowVersion` is the version the moderator saw: a decision over a newer one is a 409. */
export type ReviewStatus = 'pending' | 'approved' | 'rejected';

export interface AdminReview {
  id: number;
  productSlug: string;
  productName: string;
  name: string;
  mobile: string;
  rating: number;
  text: string;
  status: ReviewStatus;
  createdAtUtc: string;
  moderatedAtUtc: string | null;
  moderatedBy: string | null;
  rowVersion: string;
}

export interface AdminReviewList {
  items: AdminReview[];
  total: number;
  page: number;
  pageSize: number;
  pending: number;
  approved: number;
  rejected: number;
}

export interface ReviewAuditEntry {
  id: number;
  atUtc: string;
  reviewId: number;
  action: string;
  adminUsername: string | null;
  productName: string;
}

export interface ReviewAuditPage {
  items: ReviewAuditEntry[];
  total: number;
}
