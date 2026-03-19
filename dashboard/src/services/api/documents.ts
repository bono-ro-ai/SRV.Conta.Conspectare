import type {
  DocumentListResponse,
  DocumentResponse,
  UploadAcceptedResponse,
} from "../../types/api";
import { authFetch } from "./authFetch";

export interface ListDocumentsParams {
  status?: string;
  page?: number;
  pageSize?: number;
}

export async function getDocuments(
  params: ListDocumentsParams = {},
): Promise<DocumentListResponse> {
  const searchParams = new URLSearchParams();

  if (params.status) {
    searchParams.set("status", params.status);
  }
  if (params.page !== undefined) {
    searchParams.set("page", String(params.page));
  }
  if (params.pageSize !== undefined) {
    searchParams.set("pageSize", String(params.pageSize));
  }

  const query = searchParams.toString();
  const path = `/api/v1/documents${query ? `?${query}` : ""}`;

  const response = await authFetch(path);
  return (await response.json()) as DocumentListResponse;
}

export async function getDocumentById(
  id: number,
): Promise<DocumentResponse> {
  const response = await authFetch(`/api/v1/documents/${id}`);
  return (await response.json()) as DocumentResponse;
}

export async function uploadDocument(
  file: File,
  externalRef: string,
  clientReference: string,
  metadata?: string,
): Promise<UploadAcceptedResponse> {
  const formData = new FormData();
  formData.append("file", file);
  formData.append("clientReference", clientReference);
  if (metadata) {
    formData.append("metadata", metadata);
  }

  const response = await authFetch("/api/v1/documents", {
    method: "POST",
    body: formData,
    headers: {
      "X-Request-Id": externalRef,
    },
  });

  return (await response.json()) as UploadAcceptedResponse;
}

export async function retryDocument(
  id: number,
): Promise<DocumentResponse> {
  const response = await authFetch(`/api/v1/documents/${id}/retry`, {
    method: "POST",
  });
  return (await response.json()) as DocumentResponse;
}
