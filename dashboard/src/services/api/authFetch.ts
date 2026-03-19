import type { ProblemDetails } from "../../types/api";
import { API_BASE } from "./config";

const API_KEY_STORAGE_KEY = "conspectare_api_key";

export function getStoredApiKey(): string | null {
  return sessionStorage.getItem(API_KEY_STORAGE_KEY);
}

export function setStoredApiKey(apiKey: string): void {
  sessionStorage.setItem(API_KEY_STORAGE_KEY, apiKey);
}

export function clearStoredApiKey(): void {
  sessionStorage.removeItem(API_KEY_STORAGE_KEY);
}

export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly problemDetails?: ProblemDetails,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

function parseApiErrors(body: ProblemDetails): string {
  return body.detail ?? body.title ?? "An unexpected error occurred.";
}

export async function authFetch(
  path: string,
  options: RequestInit = {},
): Promise<Response> {
  const apiKey = getStoredApiKey();

  if (!apiKey) {
    window.location.href = "/login";
    throw new ApiError("Not authenticated", 401);
  }

  const url = `${API_BASE}${path}`;

  const headers = new Headers(options.headers);
  headers.set("Authorization", `Bearer ${apiKey}`);

  if (
    !(options.body instanceof FormData) &&
    !headers.has("Content-Type")
  ) {
    headers.set("Content-Type", "application/json");
  }

  const response = await fetch(url, { ...options, headers });

  if (response.status === 401) {
    clearStoredApiKey();
    window.location.href = "/login";
    throw new ApiError("Authentication expired", 401);
  }

  if (response.status === 403) {
    const body = await response.json().catch(() => null) as ProblemDetails | null;
    const message = body ? parseApiErrors(body) : "Access denied.";
    throw new ApiError(message, 403, body ?? undefined);
  }

  if (!response.ok) {
    const body = await response.json().catch(() => null) as ProblemDetails | null;
    const message = body ? parseApiErrors(body) : `Request failed with status ${response.status}`;
    throw new ApiError(message, response.status, body ?? undefined);
  }

  return response;
}
