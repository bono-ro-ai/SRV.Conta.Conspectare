import { useCallback, useEffect, useRef, useState } from "react";
import { useNavigate, useParams } from "react-router";
import { CanonicalDataTable } from "../components/CanonicalDataTable";
import { DocumentPreview } from "../components/DocumentPreview";
import { ExtractionAttemptsSection } from "../components/ExtractionAttemptsSection";
import { PipelineTimeline } from "../components/PipelineTimeline";
import { ReviewFlagsSection } from "../components/ReviewFlagsSection";
import { StatusBadge } from "../components/StatusBadge";
import { getDocumentById, retryDocument } from "../services/api/documents";
import type { DocumentResponse } from "../types/api";

const POLL_INTERVAL_MS = 3000;

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function formatTimestamp(iso: string | null): string {
  if (!iso) return "-";
  return new Date(iso).toLocaleString("ro-RO", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit",
  });
}

export function DocumentDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [document, setDocument] = useState<DocumentResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [retrying, setRetrying] = useState(false);
  const mountedRef = useRef(true);
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const documentId = id ? Number(id) : NaN;

  const fetchDocument = useCallback(async () => {
    if (isNaN(documentId)) return;
    try {
      const data = await getDocumentById(documentId);
      if (mountedRef.current) {
        setDocument(data);
        setError(null);
      }
    } catch (err) {
      if (mountedRef.current) {
        setError(err instanceof Error ? err.message : "Failed to load document.");
      }
    }
  }, [documentId]);

  useEffect(() => {
    mountedRef.current = true;
    setLoading(true);
    fetchDocument().finally(() => {
      if (mountedRef.current) {
        setLoading(false);
      }
    });
    return () => {
      mountedRef.current = false;
    };
  }, [fetchDocument]);

  useEffect(() => {
    if (!document || document.isTerminal) {
      if (pollRef.current) {
        clearInterval(pollRef.current);
        pollRef.current = null;
      }
      return;
    }
    pollRef.current = setInterval(() => {
      fetchDocument();
    }, POLL_INTERVAL_MS);
    return () => {
      if (pollRef.current) {
        clearInterval(pollRef.current);
        pollRef.current = null;
      }
    };
  }, [document?.isTerminal, fetchDocument]); // eslint-disable-line react-hooks/exhaustive-deps

  const handleBack = useCallback(() => {
    navigate("/documents");
  }, [navigate]);

  const handleRetry = useCallback(async () => {
    if (retrying) return;
    setRetrying(true);
    try {
      const data = await retryDocument(documentId);
      if (mountedRef.current) {
        setDocument(data);
      }
    } catch (err) {
      if (mountedRef.current) {
        setError(err instanceof Error ? err.message : "Retry failed.");
      }
    } finally {
      if (mountedRef.current) {
        setRetrying(false);
      }
    }
  }, [documentId, retrying]);

  if (isNaN(documentId)) {
    return (
      <div className="p-6">
        <p className="text-sm text-red-600">Invalid document ID.</p>
        <button onClick={handleBack} className="mt-2 text-sm font-medium text-indigo-600 hover:text-indigo-500">
          Back to documents
        </button>
      </div>
    );
  }

  if (loading) {
    return (
      <div className="flex items-center justify-center p-12">
        <span className="text-sm text-gray-500">Loading document...</span>
      </div>
    );
  }

  if (error && !document) {
    return (
      <div className="p-6">
        <div className="rounded-lg border border-red-200 bg-red-50 p-6">
          <p className="text-sm font-medium text-red-800">Failed to load document.</p>
          <p className="mt-1 text-sm text-red-600">{error}</p>
        </div>
        <button onClick={handleBack} className="mt-4 text-sm font-medium text-indigo-600 hover:text-indigo-500">
          Back to documents
        </button>
      </div>
    );
  }

  if (!document) {
    return (
      <div className="p-6">
        <p className="text-sm text-gray-600">Document not found.</p>
        <button onClick={handleBack} className="mt-2 text-sm font-medium text-indigo-600 hover:text-indigo-500">
          Back to documents
        </button>
      </div>
    );
  }

  const isFailed = document.status === "failed";

  return (
    <div>
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <button
            onClick={handleBack}
            className="rounded-md border border-gray-300 bg-white px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-50"
          >
            Back
          </button>
          <h2 className="text-lg font-medium text-gray-900 truncate">{document.fileName}</h2>
          <StatusBadge status={document.status} />
          {!document.isTerminal && (
            <span className="inline-flex items-center gap-1.5 text-xs text-indigo-600">
              <span className="relative flex h-2 w-2">
                <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-indigo-400 opacity-75" />
                <span className="relative inline-flex h-2 w-2 rounded-full bg-indigo-500" />
              </span>
              Watching pipeline...
            </span>
          )}
        </div>
        {isFailed && (
          <button
            onClick={handleRetry}
            disabled={retrying}
            className="rounded-md bg-indigo-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-indigo-500 disabled:opacity-50"
          >
            {retrying ? "Retrying..." : "Retry"}
          </button>
        )}
      </div>
      <div className="mt-1 flex flex-wrap gap-x-4 gap-y-1 text-xs text-gray-500">
        <span>{document.contentType}</span>
        <span>{formatFileSize(document.fileSizeBytes)}</span>
        <span>Ref: {document.externalRef}</span>
        {document.clientReference && <span>Client ref: {document.clientReference}</span>}
        <span>Created: {formatTimestamp(document.createdAt)}</span>
        {document.completedAt && <span>Completed: {formatTimestamp(document.completedAt)}</span>}
      </div>
      {error && (
        <div className="mt-2 rounded border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-700">
          {error}
        </div>
      )}
      <div className="mt-4 grid gap-6 lg:grid-cols-2">
        <div className="space-y-4">
          <div>
            <h3 className="mb-2 text-sm font-medium text-gray-700">Preview</h3>
            <DocumentPreview contentType={document.contentType} documentId={document.id} />
          </div>
        </div>
        <div className="space-y-4">
          {document.errorMessage && (
            <div className="rounded-lg border border-red-200 bg-red-50 p-3">
              <h3 className="text-xs font-semibold uppercase tracking-wide text-red-700">Error</h3>
              <p className="mt-1 text-sm text-red-600">{document.errorMessage}</p>
            </div>
          )}
          <div>
            <h3 className="mb-2 text-sm font-medium text-gray-700">Extracted Data</h3>
            <CanonicalDataTable canonicalOutputJson={document.canonicalOutputJson} />
          </div>
          <ReviewFlagsSection flags={document.reviewFlags} />
          <div>
            <h3 className="mb-2 text-sm font-medium text-gray-700">Pipeline Events</h3>
            <PipelineTimeline events={document.events} />
          </div>
          <ExtractionAttemptsSection attempts={document.extractionAttempts} />
        </div>
      </div>
    </div>
  );
}
