import type {
  DocumentListResponse,
  DocumentResponse,
  ProblemDetails,
  UploadAcceptedResponse,
} from "../../types/api";
import { authFetch } from "./authFetch";
import { clearStoredApiKey, getStoredApiKey } from "./authFetch";
import { API_BASE } from "./config";

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

export function uploadDocumentWithProgress(
  file: File,
  clientReference: string | undefined,
  onProgress: (percent: number) => void,
): Promise<UploadAcceptedResponse> {
  return new Promise((resolve, reject) => {
    const apiKey = getStoredApiKey();
    if (!apiKey) {
      window.location.href = "/login";
      reject(new Error("Not authenticated"));
      return;
    }

    const formData = new FormData();
    formData.append("file", file);
    if (clientReference) {
      formData.append("clientReference", clientReference);
    }

    const xhr = new XMLHttpRequest();
    xhr.open("POST", `${API_BASE}/api/v1/documents`);
    xhr.setRequestHeader("Authorization", `Bearer ${apiKey}`);

    xhr.upload.onprogress = (e) => {
      if (e.lengthComputable) {
        onProgress(Math.round((e.loaded / e.total) * 100));
      }
    };

    xhr.onload = () => {
      if (xhr.status === 401) {
        clearStoredApiKey();
        window.location.href = "/login";
        reject(new Error("Authentication expired"));
        return;
      }
      if (xhr.status >= 200 && xhr.status < 300) {
        resolve(JSON.parse(xhr.responseText) as UploadAcceptedResponse);
        return;
      }
      try {
        const body = JSON.parse(xhr.responseText) as ProblemDetails;
        reject(new Error(body.detail ?? body.title ?? `Upload failed with status ${xhr.status}`));
      } catch {
        reject(new Error(`Upload failed with status ${xhr.status}`));
      }
    };

    xhr.onerror = () => {
      reject(new Error("Network error during upload."));
    };

    xhr.send(formData);
  });
}
