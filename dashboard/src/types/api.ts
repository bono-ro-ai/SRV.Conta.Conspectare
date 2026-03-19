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
  pipelineStatus: string;
  documentType: string;
  retryCount: number;
  clientReference: string;
  createdAt: string;
  updatedAt: string;
  completedAt: string | null;
}

export interface DocumentEventResponse {
  id: number;
  eventType: string;
  fromStatus: string;
  toStatus: string;
  details: string | null;
  createdAt: string;
}

export interface ExtractionAttemptResponse {
  id: number;
  attemptNumber: number;
  phase: string;
  modelId: string;
  promptVersion: string;
  status: string;
  inputTokens: number | null;
  outputTokens: number | null;
  latencyMs: number | null;
  confidence: number | null;
  errorMessage: string | null;
  createdAt: string;
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
  events: DocumentEventResponse[];
  extractionAttempts: ExtractionAttemptResponse[];
  isTerminal: boolean;
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
