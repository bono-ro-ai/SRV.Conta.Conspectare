export interface ReviewFlagResponse {
  id: number;
  flagType: string;
  severity: string;
  message: string;
  isResolved: boolean;
  resolvedAt: string | null;
  createdAt: string;
}

export interface DocumentSummaryResponse {
  id: number;
  externalRef: string;
  fileName: string;
  contentType: string;
  fileSizeBytes: number;
  inputFormat: string;
  status: string;
  documentType: string;
  retryCount: number;
  clientReference: string;
  createdAt: string;
  updatedAt: string;
  completedAt: string | null;
}

export interface DocumentResponse {
  id: number;
  externalRef: string;
  fileName: string;
  contentType: string;
  fileSizeBytes: number;
  inputFormat: string;
  status: string;
  documentType: string;
  triageConfidence: number | null;
  isAccountingRelevant: boolean | null;
  retryCount: number;
  maxRetries: number;
  errorMessage: string | null;
  clientReference: string;
  metadata: string | null;
  canonicalOutputJson: string | null;
  reviewFlags: ReviewFlagResponse[];
  createdAt: string;
  updatedAt: string;
  completedAt: string | null;
}

export interface DocumentListResponse {
  items: DocumentSummaryResponse[];
  total: number;
  page: number;
  pageSize: number;
}

export interface UploadAcceptedResponse {
  id: number;
  status: string;
  createdAt: string;
}

export interface ProblemDetails {
  type: string;
  title: string;
  status: number;
  detail: string;
  instance?: string;
  traceId?: string;
}
